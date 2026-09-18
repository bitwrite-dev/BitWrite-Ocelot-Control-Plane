using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Application.UseCases.Gateway;
using DomainGateway = BitWrite.OcelotControl.Domain.Aggregates.Gateway.Gateway;
using BitWrite.OcelotControl.Domain.Events;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;

namespace BitWrite.OcelotControl.Application.UseCases.Gateway;

public class RegisterGatewayCommandHandler
{
    private readonly IGatewayRepository _gatewayRepository;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public RegisterGatewayCommandHandler(
        IGatewayRepository gatewayRepository,
        IDomainEventDispatcher eventDispatcher)
    {
        _gatewayRepository = gatewayRepository;
        _eventDispatcher = eventDispatcher;
    }

    public async Task<GatewayResponse> HandleAsync(RegisterGatewayCommand command, CancellationToken cancellationToken = default)
    {
        // Create Gateway aggregate
        var gateway = DomainGateway.Register(command.Name, command.Description);

        // Persist
        await _gatewayRepository.AddAsync(gateway, cancellationToken);

        // Dispatch domain events
        foreach (var domainEvent in gateway.DomainEvents)
        {
            await _eventDispatcher.DispatchAsync(domainEvent, cancellationToken);
        }
        gateway.ClearDomainEvents();

        // Dispatch audit event
        var auditEvent = new AuditRecorded(
            command.InitiatedBy,
            "RegisterGateway",
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