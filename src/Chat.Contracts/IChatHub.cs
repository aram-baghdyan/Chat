using MagicOnion;

namespace Chat.Contracts;

/// <summary>
/// MagicOnion StreamingHub interface for real-time chat communication.
/// </summary>
public interface IChatHub : IStreamingHub<IChatHub, IChatHubReceiver>
{
    /// <summary>
    /// Joins the chat with the specified username.
    /// </summary>
    /// <param name="username">The username to join with.</param>
    /// <param name="cancellationToken">Cancellation token for operation</param>
    Task JoinAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// Leaves the chat and disconnects.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for operation</param>
    ValueTask LeaveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a message to all connected clients.
    /// </summary>
    /// <param name="message">The message text to send.</param>
    /// <param name="cancellationToken">Cancellation token for operation</param>
    ValueTask SendMessageAsync(string message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Broadcasts a message to all connected clients.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for operation</param>
    ValueTask ServerPingAsync(CancellationToken cancellationToken = default);
}
