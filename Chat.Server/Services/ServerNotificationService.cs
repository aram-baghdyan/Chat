using Chat.Contracts;
using Chat.Server.Configuration;
using Microsoft.Extensions.Options;

namespace Chat.Server.Services;

/// <summary>
/// Background service that periodically sends server notifications to all clients.
/// Demonstrates server-initiated messaging capability.
/// </summary>
public sealed class ServerNotificationService : BackgroundService
{
    private readonly IRedisMessageBus _messageBus;
    private readonly ILogger<ServerNotificationService> _logger;
    private readonly ChatOptions _options;

    public ServerNotificationService(
        IRedisMessageBus messageBus,
        ILogger<ServerNotificationService> logger,
        IOptions<ChatOptions> options)
    {
        _messageBus = messageBus;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.EnableServerNotifications)
        {
            _logger.LogInformation("Server notifications disabled");
            return;
        }

        _logger.LogInformation(
            "Server notification service starting (interval: {Interval}s)",
            _options.ServerNotificationIntervalSeconds);

        // Wait for server to fully start
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        var interval = TimeSpan.FromSeconds(_options.ServerNotificationIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, stoppingToken);

                var notification = new MessageData
                {
                    Username = "SERVER",
                    Message = $"Server time: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC",
                    IsServerMessage = true,
                    TimestampUtc = DateTime.UtcNow
                };

                await _messageBus.PublishMessageAsync(notification, stoppingToken);

                _logger.LogDebug("Sent periodic server notification");
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending server notification");
            }
        }

        _logger.LogInformation("Server notification service stopped");
    }
}
