using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Application.UseCases.Gateway;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using DomainGateway = BitWrite.OcelotControl.Domain.Aggregates.Gateway.Gateway;

namespace BitWrite.OcelotControl.Application.UseCases.Gateway;

public class ListGatewaysQueryHandler
{
    private readonly IGatewayRepository _gatewayRepository;
    private readonly IRuntimeInstanceRepository _runtimeInstanceRepository;

    public ListGatewaysQueryHandler(
        IGatewayRepository gatewayRepository,
        IRuntimeInstanceRepository runtimeInstanceRepository)
    {
        _gatewayRepository = gatewayRepository;
        _runtimeInstanceRepository = runtimeInstanceRepository;
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

        // Read once for the page rather than per gateway, so the list stays a
        // fixed number of round trips rather than one per row.
        var heartbeats = await _runtimeInstanceRepository.GetAllAsync(cancellationToken);
        var lastSeen = heartbeats.ToDictionary(
            instance => instance.GatewayId,
            instance => instance.LastHeartbeat);

        var response = new GatewayListResponse(
            pagedGateways.Select(gateway => MapToResponse(
                gateway,
                lastSeen.TryGetValue(gateway.Id, out var beat) ? beat : null)).ToList(),
            totalCount,
            query.Page,
            query.PageSize
        );

        return response;
    }

    private static GatewayResponse MapToResponse(DomainGateway gateway, DateTimeOffset? lastHeartbeat)
    {
        return new GatewayResponse(
            gateway.Id,
            gateway.Name,
            gateway.Description,
            gateway.Status,
            gateway.CreatedAt,
            gateway.UpdatedAt,
            lastHeartbeat
        );
    }
}
