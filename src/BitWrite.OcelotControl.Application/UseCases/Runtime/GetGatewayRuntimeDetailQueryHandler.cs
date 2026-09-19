using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Application.UseCases.Runtime;
using DomainRuntimeInstance = BitWrite.OcelotControl.Domain.Aggregates.RuntimeInstance.RuntimeInstance;
using DomainRoute = BitWrite.OcelotControl.Domain.Aggregates.Route.Route;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Domain.ValueObjects.Status;
using StackExchange.Redis;

namespace BitWrite.OcelotControl.Application.UseCases.Runtime;

public class GetGatewayRuntimeDetailQueryHandler
{
    private readonly IRuntimeInstanceRepository _runtimeInstanceRepository;
    private readonly IRouteRepository _routeRepository;
    private readonly IConnectionMultiplexer _connectionMultiplexer;

    public GetGatewayRuntimeDetailQueryHandler(
        IRuntimeInstanceRepository runtimeInstanceRepository,
        IRouteRepository routeRepository,
        IConnectionMultiplexer connectionMultiplexer)
    {
        _runtimeInstanceRepository = runtimeInstanceRepository;
        _routeRepository = routeRepository;
        _connectionMultiplexer = connectionMultiplexer;
    }

    public async Task<GatewayRuntimeDetailResponse?> HandleAsync(GetGatewayRuntimeDetailQuery query, CancellationToken cancellationToken = default)
    {
        // Get runtime instance
        var runtimeInstance = await _runtimeInstanceRepository.GetAsync(query.GatewayId, cancellationToken);
        if (runtimeInstance == null)
            return null;

        // Get routes for this gateway
        var routes = await _routeRepository.GetAllAsync(cancellationToken);
        var gatewayRoutes = routes.Where(r => r.ServiceId.Value != null).ToList(); // Filter by gateway if needed

        // Get runtime info from Redis
        var db = _connectionMultiplexer.GetDatabase();
        var runtimeInfo = await db.HashGetAllAsync($"ocelot:runtime:gateway:{query.GatewayId.Value}");

        var runtimeInfoDict = runtimeInfo
            .Where(x => !x.Name.IsNullOrEmpty)
            .ToDictionary(
                x => x.Name.ToString(),
                x => x.Value.ToString()
            );

        var routesKey = $"ocelot:runtime:gateway:{query.GatewayId.Value}:routes";
        var activeRoutes = await _connectionMultiplexer.GetDatabase().SetMembersAsync(routesKey);
        var activeRoutesList = activeRoutes.Select(x => x.ToString()).ToList();

        var routeDetails = gatewayRoutes.Select(r => new RouteRuntimeDetail(
            r.Id,
            r.Key,
            r.UpstreamPath.Value,
            r.Method.Value,
            r.IsEnabled,
            r.DownstreamTargets.Select(t => t.ToUri()).ToList()
        )).ToList();

        return new GatewayRuntimeDetailResponse(
            runtimeInstance.GatewayId,
            runtimeInstance.Status,
            runtimeInstance.CurrentVersion?.Value,
            runtimeInstance.TargetVersion?.Value,
            runtimeInstance.LastHeartbeat,
            runtimeInstance.LastSynchronized,
            runtimeInstance.LastConfigApplied,
            runtimeInfoDict,
            runtimeInstance.Capabilities,
            activeRoutesList,
            routeDetails
        );
    }
}