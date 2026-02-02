namespace Chat.Contracts;

/// <summary>
/// Client-side receiver interface for receiving messages from the server.
/// </summary>
public interface IChatHubReceiver
{
    /// <summary>
    /// Called when a new message is broadcast to all clients.
    /// </summary>
    /// <param name="message">The message data.</param>
    void OnReceiveMessage(MessageData message);
}
