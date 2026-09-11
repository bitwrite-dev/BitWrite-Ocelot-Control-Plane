using BitWrite.OcelotControl.Domain.Events;

namespace BitWrite.OcelotControl.Application.Interfaces;

public interface IDomainEventHandler<in TEvent> where TEvent : DomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken = default);
}
