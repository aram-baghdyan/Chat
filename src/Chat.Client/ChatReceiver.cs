using Chat.Contracts;

/// <summary>
/// Receives and displays messages from the server.
/// </summary>
sealed class ChatReceiver : IChatHubReceiver
{
    public void OnReceiveMessage(MessageData message)
    {
        var timestamp = message.TimestampUtc.ToLocalTime().ToString("HH:mm:ss");

        if (message.IsServerMessage)
        {
            // Server messages with prefix
            var color = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[{timestamp}] [SERVER]: {message.Message}");
            Console.ForegroundColor = color;
        }
        else
        {
            // Regular user messages
            Console.WriteLine($"[{timestamp}] {message.Username}: {message.Message}");
        }
    }
}