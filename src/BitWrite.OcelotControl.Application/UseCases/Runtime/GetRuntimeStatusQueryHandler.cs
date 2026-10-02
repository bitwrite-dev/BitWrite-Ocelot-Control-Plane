using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Application.UseCases.Runtime;
using DomainRuntimeInstance = BitWrite.OcelotControl.Domain.Aggregates.RuntimeInstance.RuntimeInstance;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Domain.ValueObjects.Status;
using StackExchange.Redis;

namespace BitWrite.OcelotControl.Application.UseCases.Runtime;

public class GetRuntimeStatusQueryHandler
{
    private readonly IRuntimeInstanceRepository _runtimeInstanceRepository;
    private readonly IConnectionMultiplexer _connectionMultiplexer;

    public GetRuntimeStatusQueryHandler(
        IRuntimeInstanceRepository runtimeInstanceRepository,
        IConnectionMultiplexer connectionMultiplexer)
    {
        _runtimeInstanceRepository = runtimeInstanceRepository;
        _connectionMultiplexer = connectionMultiplexer;
    }

    public async Task<RuntimeStatusResponse?> HandleAsync(GetRuntimeStatusQuery query, CancellationToken cancellationToken = default)
    {
        // Get runtime instance from repository
        var runtimeInstance = await _runtimeInstanceRepository.GetAsync(query.GatewayId, cancellationToken);
        if (runtimeInstance == null)
            return null;

        // Get additional runtime info from Redis
        var db = _connectionMultiplexer.GetDatabase();
        var runtimeInfoDict = await GatewayRuntimeInfoReader.ReadAsync(
            db, query.GatewayId.Value);

        // Get active routes from Redis
        var routesKey = $"ocelot:runtime:gateway:{query.GatewayId.Value}:routes";
        var activeRoutes = await db.SetMembersAsync(routesKey);
        var activeRoutesList = activeRoutes.Select(x => x.ToString()).ToList();

        return new RuntimeStatusResponse(
            runtimeInstance.GatewayId,
            runtimeInstance.Status,
            runtimeInstance.CurrentVersion?.Value,
            runtimeInstance.TargetVersion?.Value,
            runtimeInstance.LastHeartbeat,
            runtimeInstance.LastSynchronized,
            runtimeInstance.LastConfigApplied,
            runtimeInfoDict,
            runtimeInstance.Capabilities,
            activeRoutesList
        );
    }
}