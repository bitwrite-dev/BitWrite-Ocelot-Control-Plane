using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Application.UseCases.Gateway;
using DomainGateway = BitWrite.OcelotControl.Domain.Aggregates.Gateway.Gateway;
using BitWrite.OcelotControl.Domain.Events;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;

namespace BitWrite.OcelotControl.Application.UseCases.Gateway;

public class UpdateGatewayCommandHandler
{
    private readonly IGatewayRepository _gatewayRepository;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public UpdateGatewayCommandHandler(
        IGatewayRepository gatewayRepository,
        IDomainEventDispatcher eventDispatcher)
    {
        _gatewayRepository = gatewayRepository;
        _eventDispatcher = eventDispatcher;
    }

    public async Task<GatewayResponse> HandleAsync(UpdateGatewayCommand command, CancellationToken cancellationToken = default)
    {
        var gateway = await _gatewayRepository.GetAsync(command.Id, cancellationToken);
        if (gateway == null)
            throw new InvalidOperationException($"Gateway {command.Id} not found");

        // Update name if provided
        if (!string.IsNullOrWhiteSpace(command.Name) && command.Name != gateway.Name)
        {
            gateway.UpdateName(command.Name);
        }

        // Update description if provided
        if (command.Description != null)
        {
            gateway.UpdateDescription(command.Description);
        }

        // Persist
        await _gatewayRepository.UpdateAsync(gateway, cancellationToken);

        // Dispatch domain events
        foreach (var domainEvent in gateway.DomainEvents)
        {
            await _eventDispatcher.DispatchAsync(domainEvent, cancellationToken);
        }
        gateway.ClearDomainEvents();

        // Dispatch audit event
        var auditEvent = new AuditRecorded(
            command.InitiatedBy,
            "UpdateGateway",
            "Gateway",
            gateway.Id.Value.ToString(),
            "Success"
        );
        await _eventDispatcher.DispatchAsync(auditEvent, cancellationToken);

        return MapToResponse(gateway);
    }

    private static GatewayResponse MapToResponse(DomainGateway gateway)
    {
        return new GatewayResponse(
            gateway.Id,
            gateway.Name,
            gateway.Description,
            gateway.Status,
            gateway.CreatedAt,
            gateway.UpdatedAt
        );
    }
}