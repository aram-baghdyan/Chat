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
    Task JoinAsync(string username);

    /// <summary>
    /// Leaves the chat and disconnects.
    /// </summary>
    ValueTask LeaveAsync();

    /// <summary>
    /// Sends a message to all connected clients.
    /// </summary>
    /// <param name="message">The message text to send.</param>
    ValueTask SendMessageAsync(string message);

    /// <summary>
    /// Broadcasts a server ping to all connected clients.
    /// </summary>
    ValueTask ServerPingAsync();

    /// <summary>
    /// Gets recent chat history (user messages only).
    /// </summary>
    /// <returns>Array of recent messages, oldest first (chronological).</returns>
    Task<MessageData[]> GetHistoryAsync();
}
