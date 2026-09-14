using AppIOutboxRepository = BitWrite.OcelotControl.Application.Interfaces.IOutboxRepository;
using AppOutboxMessage = BitWrite.OcelotControl.Application.Interfaces.OutboxMessage;
using InfraIOutboxRepository = BitWrite.OcelotControl.Infrastructure.Outbox.IOutboxRepository;
using InfraOutboxMessage = BitWrite.OcelotControl.Infrastructure.Outbox.OutboxMessage;

namespace BitWrite.OcelotControl.Infrastructure.Adapters;

/// <summary>
/// Adapter that implements Application.Interfaces.IOutboxRepository using Infrastructure.Outbox.IOutboxRepository.
/// </summary>
public class OutboxRepositoryAdapter : AppIOutboxRepository
{
    private readonly InfraIOutboxRepository _infraRepository;

    public OutboxRepositoryAdapter(InfraIOutboxRepository infraRepository)
    {
        _infraRepository = infraRepository;
    }

    public Task AddAsync(AppOutboxMessage message, CancellationToken cancellationToken = default)
    {
        var infraMessage = new InfraOutboxMessage
        {
            Id = message.Id,
            EventType = message.EventType,
            Payload = message.Payload,
            CreatedAt = message.CreatedAt.DateTime,
            RetryCount = message.RetryCount,
            Error = message.Error,
            IsProcessed = message.IsProcessed,
            ProcessedAt = message.ProcessedAt?.DateTime
        };
        return _infraRepository.AddAsync(infraMessage, cancellationToken);
    }

    public Task<IReadOnlyList<AppOutboxMessage>> GetPendingAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        return _infraRepository.GetPendingMessagesAsync(batchSize, cancellationToken)
            .ContinueWith(t => 
            {
                var result = t.Result.Select(m => new AppOutboxMessage(
                    m.Id,
                    m.EventType,
                    m.Payload,
                    new DateTimeOffset(m.CreatedAt),
                    m.RetryCount,
                    m.Error,
                    m.IsProcessed,
                    m.ProcessedAt.HasValue ? new DateTimeOffset(m.ProcessedAt.Value) : null
                )).ToList();
                return (IReadOnlyList<AppOutboxMessage>)result;
            }, cancellationToken);
    }

    public Task MarkProcessedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _infraRepository.MarkAsProcessedAsync(id, cancellationToken);
    }

    public Task MarkFailedAsync(Guid id, string error, CancellationToken cancellationToken = default)
    {
        return _infraRepository.MarkAsFailedAsync(id, error, cancellationToken);
    }

    public Task AddToDeadLetterAsync(Guid id, string error, CancellationToken cancellationToken = default)
    {
        return _infraRepository.AddToDeadLetterAsync(id, error, cancellationToken);
    }
}