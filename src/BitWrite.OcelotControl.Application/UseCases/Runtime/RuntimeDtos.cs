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

/// <summary>
/// A time range to report delivery metrics over.
/// </summary>
/// <param name="From">Inclusive start. Defaults to 24 hours ago when not supplied.</param>
/// <param name="To">Exclusive end. Defaults to now when not supplied.</param>
/// <param name="Limit">
/// Most recent attempts to consider, newest first. Bounds the work one request does,
/// since attempts are kept in a single list that is never trimmed.
/// </param>
public record GetDeliveryMetricsQuery(
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int Limit = 1000);

/// <summary>
/// How one gateway fared applying configurations.
/// </summary>
public record GatewayDeliveryMetrics(
    string GatewayId,
    int Attempts,
    int Successful,
    int Failed,
    double? SuccessRate,
    DateTimeOffset LastAttemptAt,
    IReadOnlyList<string> RecentErrors);

/// <summary>
/// One distinct failure, and how often it happened.
/// </summary>
public record DeliveryErrorFrequency(
    string Message,
    int Occurrences,
    DateTimeOffset LastSeenAt);

/// <summary>
/// What happened when configurations were delivered to gateways.
/// </summary>
/// <remarks>
/// This is configuration delivery, not request traffic. No request rate, latency or
/// error rate is reported because none is recorded: nothing in the system counts
/// requests a gateway served or times them, so there is nothing here to report and
/// no zero standing in for it. A gateway serving traffic wrongly is invisible here.
/// </remarks>
public record DeliveryMetricsResponse(
    DateTimeOffset From,
    DateTimeOffset To,
    int ConsideredAttempts,
    int TotalAttempts,
    int Successful,
    int Failed,
    double? SuccessRate,
    IReadOnlyList<int> SnapshotVersions,
    IReadOnlyList<GatewayDeliveryMetrics> Gateways,
    IReadOnlyList<DeliveryErrorFrequency> Errors);

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