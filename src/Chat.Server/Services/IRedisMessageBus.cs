using Chat.Contracts;

namespace Chat.Server.Services;

/// <summary>
/// Abstracts Redis pub/sub for distributing messages across server instances.
/// </summary>
public interface IRedisMessageBus
{
    /// <summary>
    /// Publishes a message to all server instances.
    /// </summary>
    Task PublishMessageAsync(MessageData message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to messages from all server instances.
    /// </summary>
    /// <param name="onMessage">Callback invoked when a message is received.</param>
    Task SubscribeAsync(Func<MessageData, Task> onMessage, CancellationToken cancellationToken = default);
}
