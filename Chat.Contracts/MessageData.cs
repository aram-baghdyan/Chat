using MessagePack;

namespace Chat.Contracts;

/// <summary>
/// Represents a chat message sent between client and server.
/// </summary>
[MessagePackObject]
public sealed class MessageData
{
    /// <summary>
    /// Username of the sender. Empty for server-initiated messages.
    /// </summary>
    [Key(0)]
    public string Username { get; init; } = string.Empty;

    /// <summary>
    /// The message content.
    /// </summary>
    [Key(1)]
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// When true, indicates this message originated from the server.
    /// </summary>
    [Key(2)]
    public bool IsServerMessage { get; init; }

    /// <summary>
    /// UTC timestamp when the message was created.
    /// </summary>
    [Key(3)]
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
}
