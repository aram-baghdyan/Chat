namespace Chat.Client.Configuration;

/// <summary>
/// Configuration options for the chat client.
/// </summary>
public sealed class ClientOptions
{
    /// <summary>
    /// Default server address.
    /// </summary>
    public string DefaultServerAddress { get; init; } = "http://localhost:5000";

    /// <summary>
    /// Maximum message size in bytes.
    /// </summary>
    public int MaxMessageSize { get; init; } = 4 * 1024 * 1024;

    /// <summary>
    /// Delay between reconnection attempts in milliseconds.
    /// </summary>
    public int ReconnectDelayMs { get; init; } = 3000;

    /// <summary>
    /// Maximum number of reconnection attempts before giving up.
    /// </summary>
    public int MaxReconnectAttempts { get; init; } = 10;

    /// <summary>
    /// Command to exit the chat.
    /// </summary>
    public string ExitCommand { get; init; } = "exit";
}
