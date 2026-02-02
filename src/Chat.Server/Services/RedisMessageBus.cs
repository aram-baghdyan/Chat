using System.Threading.Channels;
using Chat.Contracts;
using MessagePack;
using StackExchange.Redis;

namespace Chat.Server.Services;

/// <summary>
/// Redis-based message bus for distributing chat messages across server instances.
/// Uses pub/sub pattern for horizontal scalability.
/// </summary>
public sealed class RedisMessageBus : IRedisMessageBus, IAsyncDisposable
{
    private const string ChatChannel = "chat:messages";

    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisMessageBus> _logger;
    private readonly Channel<MessageData> _localMessageQueue;
    private readonly CancellationTokenSource _disposeCts;

    private Func<MessageData, Task>? _messageHandler;
    private Task? _subscriptionTask;

    public RedisMessageBus(
        IConnectionMultiplexer redis,
        ILogger<RedisMessageBus> logger)
    {
        _redis = redis;
        _logger = logger;
        _disposeCts = new CancellationTokenSource();

        // Bounded channel to prevent memory issues under load
        _localMessageQueue = Channel.CreateBounded<MessageData>(new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });
    }

    /// <inheritdoc />
    public async Task PublishMessageAsync(MessageData message, CancellationToken cancellationToken = default)
    {
        try
        {
            var subscriber = _redis.GetSubscriber();
            var serialized = MessagePackSerializer.Serialize(message);

            await subscriber.PublishAsync(
                RedisChannel.Literal(ChatChannel),
                serialized,
                CommandFlags.FireAndForget);

            _logger.LogDebug("Published message from {Username} to Redis", message.Username);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish message to Redis");

            // Fallback: deliver locally even if Redis fails
            await _localMessageQueue.Writer.WriteAsync(message, cancellationToken);
        }
    }

    /// <inheritdoc />
    public async Task SubscribeAsync(Func<MessageData, Task> onMessage, CancellationToken cancellationToken = default)
    {
        if (_messageHandler is not null)
        {
            throw new InvalidOperationException("Already subscribed");
        }

        _messageHandler = onMessage;

        var subscriber = _redis.GetSubscriber();

        await subscriber.SubscribeAsync(
            RedisChannel.Literal(ChatChannel),
            async (channel, value) =>
            {
                try
                {
                    var message = MessagePackSerializer.Deserialize<MessageData>(value!);
                    await _localMessageQueue.Writer.WriteAsync(message, _disposeCts.Token);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to deserialize message from Redis");
                }
            });

        _logger.LogInformation("Subscribed to Redis channel: {Channel}", ChatChannel);

        // Start background processing of messages
        _subscriptionTask = ProcessMessagesAsync(_disposeCts.Token);
    }

    private async Task ProcessMessagesAsync(CancellationToken cancellationToken)
    {
        await foreach (var message in _localMessageQueue.Reader.ReadAllAsync(cancellationToken))
        {
            try
            {
                if (_messageHandler is not null)
                {
                    await _messageHandler(message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message from {Username}", message.Username);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _disposeCts.Cancel();
        _localMessageQueue.Writer.Complete();

        if (_subscriptionTask is not null)
        {
            try
            {
                await _subscriptionTask;
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
            }
        }

        try
        {
            var subscriber = _redis.GetSubscriber();
            await subscriber.UnsubscribeAsync(RedisChannel.Literal(ChatChannel));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error unsubscribing from Redis");
        }

        _disposeCts.Dispose();
    }
}
