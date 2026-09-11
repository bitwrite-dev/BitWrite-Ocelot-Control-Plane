using BitWrite.OcelotControl.Infrastructure.Outbox;
using FluentAssertions;
using Xunit;

namespace BitWrite.OcelotControl.Application.Tests.Events;

public class OutboxRepositoryTests
{
    private readonly InMemoryOutboxRepository _repository;

    public OutboxRepositoryTests()
    {
        _repository = new InMemoryOutboxRepository();
    }

    [Fact]
    public async Task AddAsync_ShouldAddMessage()
    {
        // Arrange
        var message = new OutboxMessage
        {
            EventType = "GatewayRegistered",
            Payload = "{}"
        };

        // Act
        await _repository.AddAsync(message);

        // Assert
        var pendingMessages = await _repository.GetPendingMessagesAsync();
        pendingMessages.Should().HaveCount(1);
        pendingMessages[0].Id.Should().Be(message.Id);
    }

    [Fact]
    public async Task GetPendingMessagesAsync_ShouldReturnOnlyPendingMessages()
    {
        // Arrange
        var pendingMessage = new OutboxMessage
        {
            EventType = "GatewayRegistered",
            Payload = "{}"
        };
        var processedMessage = new OutboxMessage
        {
            EventType = "RouteCreated",
            Payload = "{}",
            IsProcessed = true,
            ProcessedAt = DateTime.UtcNow
        };

        await _repository.AddAsync(pendingMessage);
        await _repository.AddAsync(processedMessage);

        // Act
        var pendingMessages = await _repository.GetPendingMessagesAsync();

        // Assert
        pendingMessages.Should().HaveCount(1);
        pendingMessages[0].Id.Should().Be(pendingMessage.Id);
    }

    [Fact]
    public async Task MarkAsProcessedAsync_ShouldMarkMessageAsProcessed()
    {
        // Arrange
        var message = new OutboxMessage
        {
            EventType = "GatewayRegistered",
            Payload = "{}"
        };
        await _repository.AddAsync(message);

        // Act
        await _repository.MarkAsProcessedAsync(message.Id);

        // Assert
        var pendingMessages = await _repository.GetPendingMessagesAsync();
        pendingMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task MarkAsFailedAsync_ShouldIncrementRetryCount()
    {
        // Arrange
        var message = new OutboxMessage
        {
            EventType = "GatewayRegistered",
            Payload = "{}"
        };
        await _repository.AddAsync(message);

        // Act
        await _repository.MarkAsFailedAsync(message.Id, "Test error");

        // Assert
        var pendingMessages = await _repository.GetPendingMessagesAsync();
        pendingMessages.Should().HaveCount(1);
        pendingMessages[0].Error.Should().Be("Test error");
        pendingMessages[0].RetryCount.Should().Be(1);
    }
}
