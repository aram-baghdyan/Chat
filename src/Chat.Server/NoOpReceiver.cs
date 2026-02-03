using Chat.Contracts;

namespace Chat.Server;

public sealed class NoOpReceiver : IChatHubReceiver
{
    public void OnReceiveMessage(MessageData message)
    {
    }
}