using System.ComponentModel.DataAnnotations;

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
    [Range(1, 3600, ErrorMessage = "Server notification interval must be between 1 and 3600 seconds")]
    public int ServerNotificationIntervalSeconds { get; init; } = 30;

    /// <summary>
    /// Whether to send periodic server notifications.
    /// </summary>
    public bool EnableServerNotifications { get; init; } = true;

    /// <summary>
    /// Server address for internal hub client connections.
    /// </summary>
    [Required(ErrorMessage = "Server address is required")]
    public string ServerAddress { get; init; } = "http://localhost:8080";

    /// <summary>
    /// Maximum username length.
    /// </summary>
    [Range(1, 100)]
    public int MaxUsernameLength { get; init; } = 50;

    /// <summary>
    /// Maximum message length.
    /// </summary>
    [Range(1, 10000)]
    public int MaxMessageLength { get; init; } = 4000;

    /// <summary>
    /// Maximum number of messages to retain in history.
    /// </summary>
    [Range(1, 1000)]
    public int MaxHistoryMessages { get; init; } = 100;
}