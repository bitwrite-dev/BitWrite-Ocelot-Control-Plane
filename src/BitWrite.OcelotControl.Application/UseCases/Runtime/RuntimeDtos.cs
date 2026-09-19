using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Domain.ValueObjects.Status;

namespace BitWrite.OcelotControl.Application.UseCases.Runtime;

public record GetRuntimeStatusQuery(
    GatewayId GatewayId
);

public record GetAllGatewaysQuery();

public record RuntimeStatusResponse(
    GatewayId GatewayId,
    RuntimeStatus Status,
    int? CurrentVersion,
    int? TargetVersion,
    DateTimeOffset? LastHeartbeat,
    DateTimeOffset? LastSynchronized,
    DateTimeOffset? LastConfigApplied,
    IReadOnlyDictionary<string, string> RuntimeInfo,
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<string> ActiveRoutes
);

public record RuntimeGatewaysResponse(
    IReadOnlyList<RuntimeStatusResponse> Gateways
);