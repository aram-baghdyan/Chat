using Chat.Contracts;
using Chat.Server.Configuration;
using MagicOnion.Server.Hubs;
using Microsoft.Extensions.Options;

namespace Chat.Server.Hubs;

/// <summary>
/// MagicOnion StreamingHub implementation for real-time chat.
/// Thread-safe: MagicOnion ensures single-threaded execution per client connection.
/// </summary>
public sealed class ChatHub : StreamingHubBase<IChatHub, IChatHubReceiver>, IChatHub
{
    private readonly ChatOptions _options;
    private readonly ILogger<ChatHub> _logger;
    private string? _username;
    private bool _isJoined = false;
    private IGroup<IChatHubReceiver>? _group;

    public ChatHub(IOptions<ChatOptions> options, ILogger<ChatHub> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task JoinAsync(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            _logger.LogWarning("Client attempted to join with empty username");
            throw new ArgumentException("Username cannot be empty", nameof(username));
        }
        
        if (username.Length > _options.MaxUsernameLength)
        {
            _logger.LogWarning("Client attempted to join with username exceeding max length ({Length} > {MaxLength})",
                username.Length, _options.MaxUsernameLength);
            throw new ArgumentException($"Username cannot exceed {_options.MaxUsernameLength} characters", nameof(username));
        }

        _username = username;
        _isJoined = true;
        
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
            TimestampUtc = DateTime.UtcNow
        };

        _group.All.OnReceiveMessage(joinMessage);
    }

    /// <inheritdoc />
    public ValueTask LeaveAsync()
    {
        if (!_isJoined)
        {
            return ValueTask.CompletedTask;
        }

        _logger.LogInformation("User {Username} left the chat", _username);

        // Publish leave notification to all instances
        var leaveMessage = new MessageData
        {
            Username = "System",
            Message = $"{_username} left the chat",
            IsServerMessage = true,
            TimestampUtc = DateTime.UtcNow
        };
        
        SendMessagesToGroup(_group, leaveMessage);
        _isJoined = false;
        
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask SendMessageAsync(string message)
    {
        if (!_isJoined || _username is null)
        {
            _logger.LogWarning("Client attempted to send message without joining");
            throw new InvalidOperationException("Must join chat before sending messages");
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            _logger.LogDebug("User {Username} sent empty message", _username);
            return ValueTask.CompletedTask;
        }

        var messageData = new MessageData
        {
            Username = _username,
            Message = message,
            IsServerMessage = false,
            TimestampUtc = DateTime.UtcNow
        };

        _logger.LogDebug("User {Username} sending message: {Message}", _username, message);

        SendMessagesToGroup(_group, messageData);
        
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public async ValueTask ServerPingAsync()
    {
        var notificationTime = DateTime.UtcNow;
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
        
        group.All.OnReceiveMessage(message);
    }
}
