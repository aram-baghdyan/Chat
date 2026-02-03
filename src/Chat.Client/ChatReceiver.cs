using Chat.Client;
using Chat.Contracts;

namespace Chat.Client;

/// <summary>
/// Receives and displays messages from the server.
/// </summary>
public sealed class ChatReceiver : IChatHubReceiver
{
    public void OnReceiveMessage(MessageData message)
    {
        var timestamp = message.TimestampUtc.ToLocalTime().ToString("HH:mm:ss");

        if (message.IsServerMessage)
        {
            ConsoleUI.WriteSystemMessage(timestamp, message.Username, message.Message);
        }
        else
        {
            ConsoleUI.WriteUserMessage(timestamp, message.Username, message.Message);
        }
    }
}