using Chat.Contracts;
using Chat.Server.Configuration;
using Chat.Server.Services;
using FluentAssertions;
using MessagePack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace Chat.Server.Tests.Services;

public class ChatHistoryServiceTests
{
    private readonly Mock<IConnectionMultiplexer> _redisMock;
    private readonly Mock<IDatabase> _dbMock;
    private readonly Mock<ILogger<ChatHistoryService>> _loggerMock;
    private readonly ChatHistoryService _sut;

    private const string GroupName = "test-group";
    private const string ExpectedKey = $"chat:history:{GroupName}";

    public ChatHistoryServiceTests()
    {
        _redisMock = new Mock<IConnectionMultiplexer>();
        _dbMock = new Mock<IDatabase>();
        _loggerMock = new Mock<ILogger<ChatHistoryService>>();

        _redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_dbMock.Object);

        var options = Options.Create(new ChatOptions { MaxHistoryMessages = 100 });

        _sut = new ChatHistoryService(_redisMock.Object, options, _loggerMock.Object);
    }

    [Fact]
    public async Task SaveMessageAsync_ValidMessage_PushesToRedisAndTrims()
    {
        // Arrange
        var message = CreateTestMessage("user1", "Hello world");

        _dbMock.Setup(db => db.ListLeftPushAsync(
                ExpectedKey,
                It.IsAny<RedisValue>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(1);

        _dbMock.Setup(db => db.ListTrimAsync(
                ExpectedKey,
                0,
                99,
                It.IsAny<CommandFlags>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.SaveMessageAsync(GroupName, message);

        // Assert
        _dbMock.Verify(db => db.ListLeftPushAsync(
            ExpectedKey,
            It.Is<RedisValue>(v => VerifySerializedMessage(v, message)),
            When.Always,
            CommandFlags.None), Times.Once);

        _dbMock.Verify(db => db.ListTrimAsync(
            ExpectedKey,
            0,
            99,
            CommandFlags.None), Times.Once);
    }

    [Fact]
    public async Task SaveMessageAsync_RedisFailure_LogsErrorAndDoesNotThrow()
    {
        // Arrange
        var message = CreateTestMessage("user1", "Hello");

        _dbMock.Setup(db => db.ListLeftPushAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Test failure"));

        // Act
        var act = () => _sut.SaveMessageAsync(GroupName, message);

        // Assert - should not throw
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetHistoryAsync_MessagesExist_ReturnsInChronologicalOrder()
    {
        // Arrange
        var msg1 = CreateTestMessage("user1", "First message");
        var msg2 = CreateTestMessage("user2", "Second message");
        var msg3 = CreateTestMessage("user1", "Third message");

        // Redis returns newest first (LPUSH order)
        var redisValues = new RedisValue[]
        {
            MessagePackSerializer.Serialize(msg3),
            MessagePackSerializer.Serialize(msg2),
            MessagePackSerializer.Serialize(msg1)
        };

        _dbMock.Setup(db => db.ListRangeAsync(
                ExpectedKey,
                0,
                99,
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(redisValues);

        // Act
        var result = await _sut.GetHistoryAsync(GroupName);

        // Assert - should be reversed to chronological order (oldest first)
        result.Should().HaveCount(3);
        result[0].Message.Should().Be("First message");
        result[1].Message.Should().Be("Second message");
        result[2].Message.Should().Be("Third message");
    }

    [Fact]
    public async Task GetHistoryAsync_EmptyHistory_ReturnsEmptyArray()
    {
        // Arrange
        _dbMock.Setup(db => db.ListRangeAsync(
                ExpectedKey,
                0,
                99,
                It.IsAny<CommandFlags>()))
            .ReturnsAsync([]);

        // Act
        var result = await _sut.GetHistoryAsync(GroupName);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetHistoryAsync_RedisFailure_ReturnsEmptyArrayAndLogsError()
    {
        // Arrange
        _dbMock.Setup(db => db.ListRangeAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<long>(),
                It.IsAny<long>(),
                It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Test failure"));

        // Act
        var result = await _sut.GetHistoryAsync(GroupName);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetHistoryAsync_RespectsMaxHistoryConfig()
    {
        // Arrange
        var options = Options.Create(new ChatOptions { MaxHistoryMessages = 50 });
        var service = new ChatHistoryService(_redisMock.Object, options, _loggerMock.Object);

        _dbMock.Setup(db => db.ListRangeAsync(
                ExpectedKey,
                0,
                49, // MaxHistoryMessages - 1
                It.IsAny<CommandFlags>()))
            .ReturnsAsync([]);

        // Act
        await service.GetHistoryAsync(GroupName);

        // Assert
        _dbMock.Verify(db => db.ListRangeAsync(
            ExpectedKey,
            0,
            49,
            CommandFlags.None), Times.Once);
    }

    private static MessageData CreateTestMessage(string username, string message) => new()
    {
        Username = username,
        Message = message,
        IsServerMessage = false,
        TimestampUtc = DateTimeOffset.UtcNow
    };

    private static bool VerifySerializedMessage(RedisValue value, MessageData expected)
    {
        var deserialized = MessagePackSerializer.Deserialize<MessageData>((byte[])value!);
        return deserialized.Username == expected.Username &&
               deserialized.Message == expected.Message;
    }
}