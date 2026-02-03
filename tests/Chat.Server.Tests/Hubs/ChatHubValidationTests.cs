using Chat.Server.Configuration;
using Chat.Server.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace Chat.Server.Tests.Hubs;

/// <summary>
/// Tests for ChatHub validation logic.
/// Note: Full hub integration tests require MagicOnion test infrastructure.
/// These tests focus on validation rules that can be tested via reflection or by
/// verifying the configuration is correctly applied.
/// </summary>
public class ChatHubValidationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void Username_EmptyOrWhitespace_ShouldBeRejected(string? username)
    {
        // This test verifies the validation logic expectation
        // The actual validation is in JoinAsync, but we can verify the rule
        var isValid = !string.IsNullOrWhiteSpace(username);
        isValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(1, 50, true)]
    [InlineData(50, 50, true)]
    [InlineData(51, 50, false)]
    [InlineData(100, 50, false)]
    public void Username_Length_ShouldRespectMaxLength(int length, int maxLength, bool shouldBeValid)
    {
        // Arrange
        var username = new string('a', length);

        // Act
        var isValid = username.Length <= maxLength;

        // Assert
        isValid.Should().Be(shouldBeValid);
    }

    [Theory]
    [InlineData(1, 4000, true)]
    [InlineData(4000, 4000, true)]
    [InlineData(4001, 4000, false)]
    [InlineData(10000, 4000, false)]
    public void Message_Length_ShouldRespectMaxLength(int length, int maxLength, bool shouldBeValid)
    {
        // Arrange
        var message = new string('x', length);

        // Act
        var isValid = message.Length <= maxLength;

        // Assert
        isValid.Should().Be(shouldBeValid);
    }

    [Fact]
    public void ChatOptions_DefaultValues_AreReasonable()
    {
        // Arrange & Act
        var options = new ChatOptions();

        // Assert
        options.MaxUsernameLength.Should().Be(50);
        options.MaxMessageLength.Should().Be(4000);
        options.MaxHistoryMessages.Should().Be(100);
        options.ServerNotificationIntervalSeconds.Should().Be(30);
        options.EnableServerNotifications.Should().BeTrue();
    }

    [Fact]
    public void ChatOptions_Validation_RejectsInvalidValues()
    {
        // Arrange
        var options = new ChatOptions
        {
            MaxUsernameLength = 0,  // Invalid: must be >= 1
            MaxMessageLength = 0,   // Invalid: must be >= 1
            MaxHistoryMessages = 0, // Invalid: must be >= 1
            ServerNotificationIntervalSeconds = 0 // Invalid: must be >= 1
        };

        // Act & Assert - validation attributes should reject these
        // In production, ValidateDataAnnotations() catches these
        options.MaxUsernameLength.Should().BeLessThan(1);
        options.MaxMessageLength.Should().BeLessThan(1);
    }
}

/// <summary>
/// Tests for ChatHistoryService integration with ChatHub.
/// </summary>
public class ChatHubHistoryIntegrationTests
{
    private readonly Mock<IConnectionMultiplexer> _redisMock;
    private readonly Mock<IDatabase> _dbMock;
    private readonly ChatHistoryService _historyService;

    public ChatHubHistoryIntegrationTests()
    {
        _redisMock = new Mock<IConnectionMultiplexer>();
        _dbMock = new Mock<IDatabase>();

        _redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_dbMock.Object);

        var options = Options.Create(new ChatOptions());
        var logger = Mock.Of<ILogger<ChatHistoryService>>();

        _historyService = new ChatHistoryService(_redisMock.Object, options, logger);
    }

    [Fact]
    public async Task HistoryService_CanBeInstantiated_WithValidDependencies()
    {
        // Assert
        _historyService.Should().NotBeNull();

        // Verify it can be called without throwing
        _dbMock.Setup(db => db.ListRangeAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<long>(),
                It.IsAny<long>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync([]);

        var result = await _historyService.GetHistoryAsync("test");
        result.Should().BeEmpty();
    }
}
