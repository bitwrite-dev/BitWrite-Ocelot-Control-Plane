using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Application.UseCases.Service;
using DomainService = BitWrite.OcelotControl.Domain.Aggregates.Service.Service;
using BitWrite.OcelotControl.Domain.Events;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;

namespace BitWrite.OcelotControl.Application.UseCases.Service;

public class UpdateServiceCommandHandler
{
    private readonly IServiceRepository _serviceRepository;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public UpdateServiceCommandHandler(
        IServiceRepository serviceRepository,
        IDomainEventDispatcher eventDispatcher)
    {
        _serviceRepository = serviceRepository;
        _eventDispatcher = eventDispatcher;
    }

    public async Task<ServiceResponse?> HandleAsync(UpdateServiceCommand command, CancellationToken cancellationToken = default)
    {
        var service = await _serviceRepository.GetAsync(command.Id, cancellationToken);
        if (service == null)
            return null;

        // Update name if provided
        if (!string.IsNullOrWhiteSpace(command.Name) && command.Name != service.Name)
        {
            service.UpdateName(command.Name);
        }

        // Update description if provided
        if (command.Description != null)
        {
            service.UpdateDescription(command.Description);
        }

        // Persist
        await _serviceRepository.UpdateAsync(service, cancellationToken);

        // Dispatch domain events
        foreach (var domainEvent in service.DomainEvents)
        {
            await _eventDispatcher.DispatchAsync(domainEvent, cancellationToken);
        }
        service.ClearDomainEvents();

        // Dispatch audit event
        var auditEvent = new AuditRecorded(
            command.InitiatedBy,
            "UpdateService",
            "Service",
            service.Id.Value.ToString(),
            "Success"
        );
        await _eventDispatcher.DispatchAsync(auditEvent, cancellationToken);

        return MapToResponse(service);
    }

    private static ServiceResponse MapToResponse(DomainService service)
    {
        return new ServiceResponse(
            service.Id,
            service.Name,
            service.Description,
            service.CreatedAt,
            service.UpdatedAt
        );
    }
}