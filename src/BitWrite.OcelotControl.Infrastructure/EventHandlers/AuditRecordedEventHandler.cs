using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Domain.Aggregates.AuditLog;
using BitWrite.OcelotControl.Domain.Events;

namespace BitWrite.OcelotControl.Infrastructure.EventHandlers;

/// <summary>
/// Turns an <see cref="AuditRecorded"/> event into a stored audit entry.
/// </summary>
/// <remarks>
/// Every command handler already dispatched this event, and nothing handled it, so
/// the audit log was structurally complete and permanently empty: the endpoint
/// answered <c>200</c> with <c>totalCount: 0</c> no matter what the control plane did,
/// and the retention setting in #447 was a window onto a log that was never written.
///
/// Handling the event rather than writing from each command is what closes that.
/// A command cannot forget to record, because recording is not its job — and because
/// the write happens inside the dispatch, an audit entry is either written or the
/// dispatch throws, so a failure is visible instead of silent.
/// </remarks>
public class AuditRecordedEventHandler : IDomainEventHandler<AuditRecorded>
{
    private readonly IAuditLogRepository _repository;

    public AuditRecordedEventHandler(IAuditLogRepository repository) => _repository = repository;

    public async Task HandleAsync(AuditRecorded domainEvent, CancellationToken cancellationToken = default)
    {
        var entry = AuditLog.Create(
            domainEvent.Actor,
            domainEvent.Action,
            domainEvent.ResourceType,
            domainEvent.ResourceId,
            domainEvent.Result,
            correlationId: domainEvent.CorrelationId);

        await _repository.AddAsync(entry, cancellationToken);
    }
}