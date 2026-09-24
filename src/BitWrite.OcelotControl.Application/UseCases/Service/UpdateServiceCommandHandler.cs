using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Application.UseCases.Service;
using DomainService = BitWrite.OcelotControl.Domain.Aggregates.Service.Service;
using BitWrite.OcelotControl.Domain.Events;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
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

        // Update name and description if provided
        var name = !string.IsNullOrWhiteSpace(command.Name) ? command.Name : service.Name;
        var description = command.Description ?? service.Description;

        if (name != service.Name || description != service.Description)
        {
            service.Update(name, description);
        }

        // Update downstream targets if provided
        if (command.DownstreamTargets != null)
        {
            // Clear existing endpoints and add new ones
            // Note: The aggregate doesn't have a clear method, so we'll remove and re-add
            var existingEndpoints = service.Endpoints.ToList();
            
            // Remove endpoints not in the new list
            foreach (var existing in existingEndpoints)
            {
                var found = command.DownstreamTargets.Any(t => t.Host.Equals(existing.Host, StringComparison.OrdinalIgnoreCase) && t.Port == existing.Port);
                if (!found)
                {
                    try
                    {
                        service.RemoveHost(existing.Host, existing.Port);
                    }
                    catch
                    {
                        // Ignore if can't remove (e.g., last endpoint)
                    }
                }
            }

            // Add new endpoints
            foreach (var target in command.DownstreamTargets)
            {
                var existing = existingEndpoints.FirstOrDefault(e => 
                    e.Host.Equals(target.Host, StringComparison.OrdinalIgnoreCase) && e.Port == target.Port);
                
                if (existing == null)
                {
                    service.AddHost(target.Host, target.Port);
                }
            }
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
            service.Endpoints.Select(e => new ServiceEndpoint(e.Host, e.Port, e.Weight, e.IsActive)).ToList(),
            service.CreatedAt,
            service.UpdatedAt
        );
    }
}