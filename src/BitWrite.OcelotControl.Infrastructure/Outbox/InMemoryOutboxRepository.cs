using System.Collections.Concurrent;

namespace BitWrite.OcelotControl.Infrastructure.Outbox;

public class InMemoryOutboxRepository : IOutboxRepository
{
    private readonly ConcurrentDictionary<Guid, OutboxMessage> _messages = new();

    public Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        _messages.TryAdd(message.Id, message);
        return Task.CompletedTask;
    }

    public Task<List<OutboxMessage>> GetPendingMessagesAsync(int batchSize = 100, CancellationToken cancellationToken = default)
    {
        var pendingMessages = _messages.Values
            .Where(m => !m.IsProcessed)
            .OrderBy(m => m.CreatedAt)
            .Take(batchSize)
            .ToList();

        return Task.FromResult(pendingMessages);
    }

    public Task MarkAsProcessedAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        if (_messages.TryGetValue(messageId, out var message))
        {
            message.IsProcessed = true;
            message.ProcessedAt = DateTime.UtcNow;
        }

        return Task.CompletedTask;
    }

    public Task MarkAsFailedAsync(Guid messageId, string error, CancellationToken cancellationToken = default)
    {
        if (_messages.TryGetValue(messageId, out var message))
        {
            message.Error = error;
            message.RetryCount++;
        }

        return Task.CompletedTask;
    }
}
