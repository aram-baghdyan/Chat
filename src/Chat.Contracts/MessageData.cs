using MessagePack;

namespace Chat.Contracts;

/// <summary>
/// Represents a chat message sent between client and server.
/// </summary>
[MessagePackObject]
public sealed record MessageData
{
    /// <summary>
    /// MessageId of the message.
    /// </summary>
    [Key(0)]                                                                                                                                                                                             
    public Guid MessageId { get; } = Guid.NewGuid();   
    
    /// <summary>
    /// Username of the sender or server name if originated from server.
    /// </summary>
    [Key(1)]
    public required string Username { get; init; }

    /// <summary>
    /// The message content.
    /// </summary>
    [Key(2)]
    public required string Message { get; init; }

    /// <summary>
    /// When true, indicates this message originated from the server.
    /// </summary>
    [Key(3)]
    public required bool IsServerMessage { get; init; }

    /// <summary>
    /// UTC timestamp when the message was created.
    /// </summary>
    [Key(4)]
    public required DateTimeOffset TimestampUtc { get; init; }
}
