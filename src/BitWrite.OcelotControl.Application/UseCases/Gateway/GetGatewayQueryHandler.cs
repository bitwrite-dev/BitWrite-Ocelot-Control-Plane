using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Application.UseCases.Gateway;
using DomainGateway = BitWrite.OcelotControl.Domain.Aggregates.Gateway.Gateway;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;

namespace BitWrite.OcelotControl.Application.UseCases.Gateway;

public class GetGatewayQueryHandler
{
    private readonly IGatewayRepository _gatewayRepository;

    public GetGatewayQueryHandler(IGatewayRepository gatewayRepository)
    {
        _gatewayRepository = gatewayRepository;
    }

    public async Task<GatewayResponse?> HandleAsync(GetGatewayQuery query, CancellationToken cancellationToken = default)
    {
        var gateway = await _gatewayRepository.GetAsync(query.Id, cancellationToken);
        return gateway != null ? MapToResponse(gateway) : null;
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