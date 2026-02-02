using Chat.Contracts;

namespace Chat.Server;

public class NoOpReceiver : IChatHubReceiver
{
    public void OnReceiveMessage(MessageData message)
    {
    }
}