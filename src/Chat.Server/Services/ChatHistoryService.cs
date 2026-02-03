using Chat.Contracts;
using Chat.Server.Configuration;
using MessagePack;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Chat.Server.Services;

/// <summary>
/// Manages chat message history storage in Redis.
/// </summary>
public sealed class ChatHistoryService
{
    private readonly IDatabase _db;
    private readonly int _maxMessages;
    private readonly ILogger<ChatHistoryService> _logger;

    public ChatHistoryService(
        IConnectionMultiplexer redis,
        IOptions<ChatOptions> options,
        ILogger<ChatHistoryService> logger)
    {
        _db = redis.GetDatabase();
        _maxMessages = options.Value.MaxHistoryMessages;
        _logger = logger;
    }

    /// <summary>
    /// Saves a message to the history for the specified group.
    /// </summary>
    public async Task SaveMessageAsync(string groupName, MessageData message)
    {
        var key = GetHistoryKey(groupName);

        try
        {
            var serialized = MessagePackSerializer.Serialize(message);
            await _db.ListLeftPushAsync(key, serialized);
            await _db.ListTrimAsync(key, 0, _maxMessages - 1);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save message to history for group {GroupName}", groupName);
        }
    }

    /// <summary>
    /// Gets the message history for the specified group.
    /// </summary>
    /// <returns>Messages in chronological order (oldest first).</returns>
    public async Task<MessageData[]> GetHistoryAsync(string groupName)
    {
        var key = GetHistoryKey(groupName);

        try
        {
            var values = await _db.ListRangeAsync(key, 0, _maxMessages - 1);

            if (values.Length == 0)
            {
                return [];
            }

            var messages = values
                .Where(v => v.HasValue)
                .Select(v => MessagePackSerializer.Deserialize<MessageData>((byte[])v!))
                .Reverse()  // Oldest first for chronological display
                .ToArray();

            _logger.LogDebug("Retrieved {Count} messages from history for group {GroupName}",
                messages.Length, groupName);

            return messages;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve history for group {GroupName}", groupName);
            return [];
        }
    }

    private static string GetHistoryKey(string groupName) => $"chat:history:{groupName}";
}