using Chat.Contracts;
using Chat.Server.Configuration;
using Grpc.Net.Client;
using MagicOnion.Client;
using Microsoft.Extensions.Options;

namespace Chat.Server.Services;

/// <summary>
/// Background service that periodically sends server notifications to all clients.
/// Demonstrates server-initiated messaging capability.
/// </summary>
public sealed class ServerNotificationService : BackgroundService
{
    private readonly ILogger<ServerNotificationService> _logger;
    private readonly ChatOptions _options;

    public ServerNotificationService(
        IOptions<ChatOptions> options,
        ILogger<ServerNotificationService> logger)
    {
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
                await RunNotificationLoopAsync(interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Notification loop failed, reconnecting in 5s");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _logger.LogInformation("Server notification service stopped");
    }

    private static async Task RunNotificationLoopAsync(TimeSpan interval, CancellationToken cancellationToken)
    {
        var channel = GrpcChannel.ForAddress("http://localhost:8080", new GrpcChannelOptions
        {
            MaxReceiveMessageSize = 4 * 1024 * 1024,
            MaxSendMessageSize = 4 * 1024 * 1024
        });
        var hub = await StreamingHubClient.ConnectAsync<IChatHub, IChatHubReceiver>(
            channel, new NoOpReceiver(), cancellationToken: cancellationToken);
        
        try
        {
            using var timer = new PeriodicTimer(interval);                                                                                                                                                           
            while (await timer.WaitForNextTickAsync(cancellationToken))                                                                                                                                                  
            {   
                hub.ServerPingAsync();
            }
        }
        finally
        {
            await hub.DisposeAsync();
        }
    }
}
