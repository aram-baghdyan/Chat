using System.Collections.Concurrent;
using Chat.Contracts;
using Chat.Server.Services;
using MagicOnion.Server.Hubs;
using MessagePack;

namespace Chat.Server.Hubs;

/// <summary>
/// MagicOnion StreamingHub implementation for real-time chat.
/// Thread-safe: MagicOnion ensures single-threaded execution per client connection.
/// </summary>
public sealed class ChatHub : StreamingHubBase<IChatHub, IChatHubReceiver>, IChatHub
{
    private readonly ILogger<ChatHub> _logger;
    private readonly IRedisMessageBus _messageBus;
    private static readonly ConcurrentDictionary<string, IGroup<IChatHubReceiver>> _globalGroups = new();

    private string _username = string.Empty;
    private bool _isJoined;
    private IGroup<IChatHubReceiver>? _myGroup;

    public ChatHub(
        ILogger<ChatHub> logger,
        IRedisMessageBus messageBus)
    {
        _logger = logger;
        _messageBus = messageBus;
    }

    /// <summary>
    /// Called by MessageBroadcaster to broadcast messages to all connected clients.
    /// </summary>
    internal static void BroadcastToAll(MessageData message)
    {
        // Broadcast to all tracked group instances
        foreach (var group in _globalGroups.Values)
        {
            try
            {
                group.All.OnReceiveMessage(message);
            }
            catch
            {
                // Ignore broadcast errors for individual groups
            }
        }
    }

    /// <inheritdoc />
    public async Task JoinAsync(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            _logger.LogWarning("Client attempted to join with empty username");
            throw new ArgumentException("Username cannot be empty", nameof(username));
        }

        if (_isJoined)
        {
            _logger.LogWarning("Client {Username} attempted to join twice", _username);
            return;
        }

        _username = username;
        _isJoined = true;

        // Add to global broadcast group and store reference
        _myGroup = await Group.AddAsync("Global");

        // Track this group for broadcasting from Redis messages
        _globalGroups.TryAdd(Context.ContextId.ToString(), _myGroup);

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

        await _messageBus.PublishMessageAsync(joinMessage);
    }

    /// <inheritdoc />
    public async Task LeaveAsync()
    {
        if (!_isJoined)
        {
            return;
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

        await _messageBus.PublishMessageAsync(leaveMessage);

        _isJoined = false;
    }

    /// <inheritdoc />
    public async Task SendMessageAsync(string message)
    {
        if (!_isJoined)
        {
            _logger.LogWarning("Client attempted to send message without joining");
            throw new InvalidOperationException("Must join chat before sending messages");
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            _logger.LogDebug("User {Username} sent empty message", _username);
            return;
        }

        var messageData = new MessageData
        {
            Username = _username,
            Message = message,
            IsServerMessage = false,
            TimestampUtc = DateTime.UtcNow
        };

        _logger.LogDebug("User {Username} sending message: {Message}", _username, message);

        // Publish to Redis for distribution across all server instances
        await _messageBus.PublishMessageAsync(messageData);
    }

    protected override async ValueTask OnDisconnected()
    {
        // Remove from tracking
        _globalGroups.TryRemove(Context.ContextId.ToString(), out _);

        if (_isJoined)
        {
            await LeaveAsync();
        }

        await base.OnDisconnected();
    }
}
