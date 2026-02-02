using Chat.Contracts;
using Chat.Server.Services;

namespace Chat.Server.Services;

/// <summary>
/// Background service that receives messages from Redis and broadcasts to connected clients.
/// Note: This implementation stores the broadcast callback which will be set by Program.cs
/// after MagicOnion groups are initialized.
/// </summary>
public sealed class MessageBroadcaster : BackgroundService
{
    private readonly IRedisMessageBus _messageBus;
    private readonly ILogger<MessageBroadcaster> _logger;

    private static Action<MessageData>? _broadcastCallback;

    public MessageBroadcaster(
        IRedisMessageBus messageBus,
        ILogger<MessageBroadcaster> logger)
    {
        _messageBus = messageBus;
        _logger = logger;
    }

    /// <summary>
    /// Sets the callback used to broadcast messages to all connected clients.
    /// This should be called from Program.cs after MagicOnion is fully initialized.
    /// </summary>
    public static void SetBroadcastCallback(Action<MessageData> callback)
    {
        _broadcastCallback = callback;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Message broadcaster starting");

        // Wait a moment for the server to fully initialize
        await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);

        await _messageBus.SubscribeAsync(message =>
        {
            try
            {
                if (_broadcastCallback != null)
                {
                    _broadcastCallback(message);

                    _logger.LogDebug(
                        "Broadcast message from {Username}",
                        message.Username);
                }
                else
                {
                    _logger.LogWarning("Broadcast callback not set");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to broadcast message");
            }

            return Task.CompletedTask;
        }, stoppingToken);

        _logger.LogInformation("Message broadcaster running");

        // Keep service alive
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Message broadcaster stopping");
        }
    }
}
