using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Application.UseCases.Runtime;
using DomainRuntimeInstance = BitWrite.OcelotControl.Domain.Aggregates.RuntimeInstance.RuntimeInstance;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Domain.ValueObjects.Status;
using StackExchange.Redis;

namespace BitWrite.OcelotControl.Application.UseCases.Runtime;

public class GetAllGatewaysQueryHandler
{
    private readonly IRuntimeInstanceRepository _runtimeInstanceRepository;
    private readonly IConnectionMultiplexer _connectionMultiplexer;

    public GetAllGatewaysQueryHandler(
        IRuntimeInstanceRepository runtimeInstanceRepository,
        IConnectionMultiplexer connectionMultiplexer)
    {
        _runtimeInstanceRepository = runtimeInstanceRepository;
        _connectionMultiplexer = connectionMultiplexer;
    }

    public async Task<RuntimeGatewaysResponse> HandleAsync(GetAllGatewaysQuery query, CancellationToken cancellationToken = default)
    {
        var runtimeInstances = await _runtimeInstanceRepository.GetAllAsync(cancellationToken);

        var gatewayStatuses = new List<RuntimeStatusResponse>();

        foreach (var instance in runtimeInstances)
        {
            var db = _connectionMultiplexer.GetDatabase();
            var runtimeKey = $"ocelot:runtime:gateway:{instance.GatewayId.Value}";
            var runtimeInfo = await db.HashGetAllAsync(runtimeKey);

            var runtimeInfoDict = runtimeInfo
                .Where(x => !x.Name.IsNullOrEmpty)
                .ToDictionary(
                    x => x.Name.ToString(),
                    x => x.Value.ToString()
                );

            var routesKey = $"ocelot:runtime:gateway:{instance.GatewayId.Value}:routes";
            var activeRoutes = await db.SetMembersAsync(routesKey);
            var activeRoutesList = activeRoutes.Select(x => x.ToString()).ToList();

            gatewayStatuses.Add(new RuntimeStatusResponse(
                instance.GatewayId,
                instance.Status,
                instance.CurrentVersion?.Value,
                instance.TargetVersion?.Value,
                instance.LastHeartbeat,
                instance.LastSynchronized,
                instance.LastConfigApplied,
                runtimeInfoDict,
                instance.Capabilities,
                activeRoutesList
            ));
        }

        return new RuntimeGatewaysResponse(gatewayStatuses);
    }
}