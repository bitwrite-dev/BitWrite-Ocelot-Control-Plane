using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Application.UseCases.Gateway;
using DomainGateway = BitWrite.OcelotControl.Domain.Aggregates.Gateway.Gateway;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;

namespace BitWrite.OcelotControl.Application.UseCases.Gateway;

public class ListGatewaysQueryHandler
{
    private readonly IGatewayRepository _gatewayRepository;

    public ListGatewaysQueryHandler(IGatewayRepository gatewayRepository)
    {
        _gatewayRepository = gatewayRepository;
    }

    public async Task<GatewayListResponse> HandleAsync(ListGatewaysQuery query, CancellationToken cancellationToken = default)
    {
        var gateways = await _gatewayRepository.GetAllAsync(cancellationToken);

        var totalCount = gateways.Count;
        var pagedGateways = gateways
            .OrderBy(g => g.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();

        var response = new GatewayListResponse(
            pagedGateways.Select(MapToResponse).ToList(),
            totalCount,
            query.Page,
            query.PageSize
        );

        return response;
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