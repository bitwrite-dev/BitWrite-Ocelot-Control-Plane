namespace BitWrite.OcelotControl.Application.Interfaces;

public record OutboxMessage(
    Guid Id,
    string EventType,
    string Payload,
    DateTimeOffset CreatedAt,
    int RetryCount = 0,
    string? Error = null,
    bool IsProcessed = false,
    DateTimeOffset? ProcessedAt = null);

public interface IOutboxRepository
{
    Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(int batchSize, CancellationToken cancellationToken = default);
    Task MarkProcessedAsync(Guid id, CancellationToken cancellationToken = default);
    Task MarkFailedAsync(Guid id, string error, CancellationToken cancellationToken = default);
    Task AddToDeadLetterAsync(Guid id, string error, CancellationToken cancellationToken = default);
}