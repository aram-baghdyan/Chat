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
    Task LeaveAsync();

    /// <summary>
    /// Sends a message to all connected clients.
    /// </summary>
    /// <param name="message">The message text to send.</param>
    Task SendMessageAsync(string message);
}
