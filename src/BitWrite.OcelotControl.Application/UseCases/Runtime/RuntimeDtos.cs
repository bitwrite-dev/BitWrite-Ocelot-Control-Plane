using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Domain.ValueObjects.Status;

namespace BitWrite.OcelotControl.Application.UseCases.Runtime;

public record GetRuntimeStatusQuery(
    GatewayId GatewayId
);

public record GetAllGatewaysQuery();

public record GetGatewayRuntimeDetailQuery(
    GatewayId GatewayId
);

public record ReconcileGatewayCommand(
    GatewayId GatewayId,
    SnapshotVersion TargetVersion,
    string InitiatedBy,
    string CorrelationId = ""
);

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

public record GatewayRuntimeDetailResponse(
    GatewayId GatewayId,
    RuntimeStatus Status,
    int? CurrentVersion,
    int? TargetVersion,
    DateTimeOffset? LastHeartbeat,
    DateTimeOffset? LastSynchronized,
    DateTimeOffset? LastConfigApplied,
    IReadOnlyDictionary<string, string> RuntimeInfo,
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<string> ActiveRoutes,
    IReadOnlyList<RouteRuntimeDetail> Routes
);

public record RouteRuntimeDetail(
    RouteId RouteId,
    string Key,
    string UpstreamPath,
    string Method,
    bool IsEnabled,
    IReadOnlyList<string> DownstreamTargets
);

public record ReconcileGatewayResponse(
    GatewayId GatewayId,
    SnapshotVersion TargetVersion,
    bool Success,
    string? ErrorMessage,
    DateTimeOffset ReconciledAt
);