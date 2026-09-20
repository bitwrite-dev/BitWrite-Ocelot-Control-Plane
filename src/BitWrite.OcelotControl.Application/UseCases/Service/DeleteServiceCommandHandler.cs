using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Application.UseCases.Service;
using BitWrite.OcelotControl.Domain.Aggregates.Service;
using BitWrite.OcelotControl.Domain.Events;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;

namespace BitWrite.OcelotControl.Application.UseCases.Service;

public class DeleteServiceCommandHandler
{
    private readonly IServiceRepository _serviceRepository;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public DeleteServiceCommandHandler(
        IServiceRepository serviceRepository,
        IDomainEventDispatcher eventDispatcher)
    {
        _serviceRepository = serviceRepository;
        _eventDispatcher = eventDispatcher;
    }

    public async Task<bool> HandleAsync(DeleteServiceCommand command, CancellationToken cancellationToken = default)
    {
        var service = await _serviceRepository.GetAsync(command.Id, cancellationToken);
        if (service == null)
            return false;

        service.Delete();

        // Persist deletion
        await _serviceRepository.DeleteAsync(command.Id, cancellationToken);

        // Dispatch domain events from aggregate
        foreach (var domainEvent in service.DomainEvents)
        {
            await _eventDispatcher.DispatchAsync(domainEvent, cancellationToken);
        }
        service.ClearDomainEvents();

        // Dispatch audit event
        var auditEvent = new AuditRecorded(
            command.InitiatedBy,
            "DeleteService",
            "Service",
            command.Id.Value.ToString(),
            "Success"
        );
        await _eventDispatcher.DispatchAsync(auditEvent, cancellationToken);

        return true;
    }
}