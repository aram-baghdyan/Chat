namespace Chat.Server.Configuration;

/// <summary>
/// Configuration options for chat server behavior.
/// </summary>
public sealed class ChatOptions
{
    public const string SectionName = "Chat";

    /// <summary>
    /// Interval in seconds between server notifications.
    /// </summary>
    public int ServerNotificationIntervalSeconds { get; init; } = 30;

    /// <summary>
    /// Whether to send periodic server notifications.
    /// </summary>
    public bool EnableServerNotifications { get; init; } = true;
}
