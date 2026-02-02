using Chat.Contracts;
using Chat.Server.Hubs;

namespace Chat.Server.Services;

/// <summary>
/// Background service that receives messages from Redis and broadcasts to connected clients.
/// </summary>
public sealed class MessageBroadcaster : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MessageBroadcaster> _logger;

    public MessageBroadcaster(
        IServiceProvider serviceProvider,
        ILogger<MessageBroadcaster> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Message broadcaster starting");

        // Wait a moment for the server to fully initialize
        await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);

        using var scope = _serviceProvider.CreateScope();
        var messageBus = scope.ServiceProvider.GetRequiredService<IRedisMessageBus>();

        await messageBus.SubscribeAsync(message =>
        {
            try
            {
                ChatHub.BroadcastToAll(message);

                _logger.LogDebug(
                    "Broadcast message from {Username}",
                    message.Username);
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
