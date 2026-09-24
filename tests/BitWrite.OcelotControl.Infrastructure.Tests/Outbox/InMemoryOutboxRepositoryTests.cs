using BitWrite.OcelotControl.Infrastructure.Outbox;
using FluentAssertions;
using Xunit;

namespace BitWrite.OcelotControl.Infrastructure.Tests.Outbox;

public class InMemoryOutboxRepositoryTests
{
    private readonly InMemoryOutboxRepository _repository;

    public InMemoryOutboxRepositoryTests()
    {
        _repository = new InMemoryOutboxRepository();
    }

    [Fact]
    public async Task AddAsync_ShouldAddMessage()
    {
        var message = new OutboxMessage
        {
            EventType = "GatewayRegistered",
            Payload = "{}"
        };

        await _repository.AddAsync(message);

        var pendingMessages = await _repository.GetPendingMessagesAsync();
        pendingMessages.Should().HaveCount(1);
        pendingMessages[0].Id.Should().Be(message.Id);
    }

    [Fact]
    public async Task GetPendingMessagesAsync_ShouldReturnOnlyPendingMessages()
    {
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

        var pendingMessages = await _repository.GetPendingMessagesAsync();

        pendingMessages.Should().HaveCount(1);
        pendingMessages[0].Id.Should().Be(pendingMessage.Id);
    }

    [Fact]
    public async Task MarkAsProcessedAsync_ShouldMarkMessageAsProcessed()
    {
        var message = new OutboxMessage
        {
            EventType = "GatewayRegistered",
            Payload = "{}"
        };
        await _repository.AddAsync(message);

        await _repository.MarkAsProcessedAsync(message.Id);

        var pendingMessages = await _repository.GetPendingMessagesAsync();
        pendingMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task MarkAsFailedAsync_ShouldIncrementRetryCount()
    {
        var message = new OutboxMessage
        {
            EventType = "GatewayRegistered",
            Payload = "{}"
        };
        await _repository.AddAsync(message);

        await _repository.MarkAsFailedAsync(message.Id, "Test error");

        var pendingMessages = await _repository.GetPendingMessagesAsync();
        pendingMessages.Should().HaveCount(1);
        pendingMessages[0].Error.Should().Be("Test error");
        pendingMessages[0].RetryCount.Should().Be(1);
    }

    [Fact]
    public async Task GetPendingMessagesAsync_ShouldReturnMessagesInOrder()
    {
        var message1 = new OutboxMessage { EventType = "Event1", Payload = "{}" };
        var message2 = new OutboxMessage { EventType = "Event2", Payload = "{}" };
        var message3 = new OutboxMessage { EventType = "Event3", Payload = "{}" };

        await _repository.AddAsync(message1);
        await _repository.AddAsync(message2);
        await _repository.AddAsync(message3);

        var messages = await _repository.GetPendingMessagesAsync();

        messages.Should().HaveCount(3);
        messages[0].EventType.Should().Be("Event1");
        messages[1].EventType.Should().Be("Event2");
        messages[2].EventType.Should().Be("Event3");
    }
}