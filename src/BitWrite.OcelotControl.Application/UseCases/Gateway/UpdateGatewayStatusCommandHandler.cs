using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Application.UseCases.Gateway;
using DomainGateway = BitWrite.OcelotControl.Domain.Aggregates.Gateway.Gateway;
using BitWrite.OcelotControl.Domain.Events;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Domain.ValueObjects.Status;

namespace BitWrite.OcelotControl.Application.UseCases.Gateway;

public class UpdateGatewayStatusCommandHandler
{
    private readonly IGatewayRepository _gatewayRepository;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public UpdateGatewayStatusCommandHandler(
        IGatewayRepository gatewayRepository,
        IDomainEventDispatcher eventDispatcher)
    {
        _gatewayRepository = gatewayRepository;
        _eventDispatcher = eventDispatcher;
    }

    public async Task<GatewayResponse?> HandleAsync(UpdateGatewayStatusCommand command, CancellationToken cancellationToken = default)
    {
        var gateway = await _gatewayRepository.GetAsync(command.Id, cancellationToken);
        if (gateway == null)
            return null;

        // Update status
        gateway.SetStatus(command.Status);

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
            "UpdateGatewayStatus",
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