using Chat.Contracts;
using Chat.Server.Configuration;
using Chat.Server.Services;
using MagicOnion.Server.Hubs;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Chat.Server.Hubs;

/// <summary>
/// MagicOnion StreamingHub implementation for real-time chat.
/// Thread-safe: MagicOnion ensures single-threaded execution per client connection.
/// </summary>
public sealed class ChatHub : StreamingHubBase<IChatHub, IChatHubReceiver>, IChatHub
{
    private static readonly Meter Meter = new("Chat.Server", "1.0.0");

    private static readonly UpDownCounter<int> ActiveConnections =
        Meter.CreateUpDownCounter<int>("chat.connections.active",
            description: "Number of currently connected users");

    private static readonly Counter<long> JoinsTotal =
        Meter.CreateCounter<long>("chat.joins.total",
            description: "Total join attempts");

    private static readonly Counter<long> MessagesSent =
        Meter.CreateCounter<long>("chat.messages.sent",
            description: "Total messages broadcast");

    private static readonly Histogram<long> MessageBytes =
        Meter.CreateHistogram<long>("chat.messages.bytes",
            unit: "By",
            description: "Size of broadcast message payloads");

    private static readonly Histogram<double> BroadcastDuration =
        Meter.CreateHistogram<double>("chat.group.broadcast.duration",
            unit: "ms",
            description: "Time to broadcast a message through the Redis group");

    private readonly ChatOptions _options;
    private readonly ILogger<ChatHub> _logger;
    private readonly ChatHistoryService _historyService;
    private string? _username;
    private bool _isJoined;
    private IGroup<IChatHubReceiver>? _group;

    public ChatHub(
        IOptions<ChatOptions> options,
        ILogger<ChatHub> logger,
        ChatHistoryService historyService)
    {
        _options = options.Value;
        _logger = logger;
        _historyService = historyService;
    }

    /// <inheritdoc />
    public async Task JoinAsync(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            _logger.LogWarning("Client attempted to join with empty username");
            JoinsTotal.Add(1, new KeyValuePair<string, object?>("outcome", "rejected"));
            throw new ArgumentException("Username cannot be empty", nameof(username));
        }

        if (username.Length > _options.MaxUsernameLength)
        {
            _logger.LogWarning("Client attempted to join with username exceeding max length ({Length} > {MaxLength})",
                username.Length, _options.MaxUsernameLength);
            JoinsTotal.Add(1, new KeyValuePair<string, object?>("outcome", "rejected"));
            throw new ArgumentException($"Username cannot exceed {_options.MaxUsernameLength} characters", nameof(username));
        }

        _username = username;
        _isJoined = true;
        JoinsTotal.Add(1, new KeyValuePair<string, object?>("outcome", "success"));
        ActiveConnections.Add(1);

        // Add to global broadcast group and store reference
        _group = await Group.AddAsync(Constants.GlobalGroupName);

        _logger.LogInformation("User {Username} joined the chat (ContextId: {ContextId})",
            _username, Context.ContextId);

        // Publish join notification to all instances
        var joinMessage = new MessageData
        {
            Username = "System",
            Message = $"{_username} joined the chat",
            IsServerMessage = true,
            TimestampUtc = DateTimeOffset.UtcNow
        };

        SendMessagesToGroup(_group, joinMessage);
    }

    /// <inheritdoc />
    public ValueTask LeaveAsync()
    {
        if (!_isJoined)
        {
            return ValueTask.CompletedTask;
        }

        _logger.LogInformation("User {Username} left the chat", _username);
        ActiveConnections.Add(-1);

        // Publish leave notification to all instances
        var leaveMessage = new MessageData
        {
            Username = "System",
            Message = $"{_username} left the chat",
            IsServerMessage = true,
            TimestampUtc = DateTimeOffset.UtcNow
        };
        
        SendMessagesToGroup(_group, leaveMessage);
        _isJoined = false;
        
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask SendMessageAsync(string message)
    {
        if (!_isJoined || _username is null)
        {
            _logger.LogWarning("Client attempted to send message without joining");
            throw new InvalidOperationException("Must join chat before sending messages");
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            _logger.LogDebug("User {Username} sent empty message", _username);
            return;
        }

        if (message.Length > _options.MaxMessageLength)
        {
            _logger.LogWarning("User {Username} attempted to send message exceeding max length ({Length} > {MaxLength})",
                _username, message.Length, _options.MaxMessageLength);
            throw new ArgumentException($"Message cannot exceed {_options.MaxMessageLength} characters", nameof(message));
        }

        var messageData = new MessageData
        {
            Username = _username,
            Message = message,
            IsServerMessage = false,
            TimestampUtc = DateTimeOffset.UtcNow
        };

        _logger.LogDebug("User {Username} sending message: {Message}", _username, message);

        // Broadcast to all clients
        SendMessagesToGroup(_group, messageData);

        // Save to history (fire-and-forget, non-critical)
        await _historyService.SaveMessageAsync(Constants.GlobalGroupName, messageData);
    }

    /// <inheritdoc />
    public async ValueTask ServerPingAsync()
    {
        var notificationTime = DateTimeOffset.UtcNow;
        _group ??= await Group.AddAsync(Constants.GlobalGroupName);

        SendMessagesToGroup(
            _group,
            new MessageData
            {
                Username = $"SERVER:{Environment.MachineName}",
                Message = $"Server time: {notificationTime:yyyy-MM-dd HH:mm:ss} UTC",
                TimestampUtc = notificationTime,
                IsServerMessage = true,
            });
    }

    /// <inheritdoc />
    public async Task<MessageData[]> GetHistoryAsync()
    {
        return await _historyService.GetHistoryAsync(Constants.GlobalGroupName);
    }

    protected override async ValueTask OnDisconnected()
    {
        if (_isJoined)
        {
            await LeaveAsync();
        }

        await base.OnDisconnected();
    }

    private static void SendMessagesToGroup(IGroup<IChatHubReceiver>? group, MessageData message)
    {
        if (group is null)
        {
            throw new InvalidOperationException("Group not initialized");
        }

        var attr = new KeyValuePair<string, object?>("type", message.IsServerMessage ? "system" : "user");
        MessagesSent.Add(1, attr);
        MessageBytes.Record(message.Message.Length, attr);

        var sw = Stopwatch.StartNew();
        group.All.OnReceiveMessage(message);
        BroadcastDuration.Record(sw.Elapsed.TotalMilliseconds, attr);
    }
}
