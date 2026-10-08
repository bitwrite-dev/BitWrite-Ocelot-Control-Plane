using System.ComponentModel.DataAnnotations;

namespace BitWrite.OcelotControl.Api.DTOs;

public record RuntimeStatusResponse(
    string GatewayId,
    string Status,
    int? CurrentVersion,
    int? TargetVersion,
    DateTimeOffset? LastHeartbeat,
    DateTimeOffset? LastSynchronized,
    DateTimeOffset? LastConfigApplied,
    Dictionary<string, string> RuntimeInfo,
    List<string> Capabilities,
    List<string> ActiveRoutes
);

public record RuntimeGatewaysResponse(
    List<RuntimeStatusResponse> Gateways
);

/// <summary>How one gateway fared applying configurations.</summary>
public record GatewayDeliveryMetrics(
    string GatewayId,
    int Attempts,
    int Successful,
    int Failed,
    double? SuccessRate,
    DateTimeOffset LastAttemptAt,
    List<string> RecentErrors);

/// <summary>One distinct delivery failure, and how often it happened.</summary>
public record DeliveryErrorFrequency(
    string Message,
    int Occurrences,
    DateTimeOffset LastSeenAt);

/// <summary>How configuration delivery to gateways has been going.</summary>
public record DeliveryMetricsResponse(
    DateTimeOffset From,
    DateTimeOffset To,
    int ConsideredAttempts,
    int TotalAttempts,
    int Successful,
    int Failed,
    double? SuccessRate,
    List<int> SnapshotVersions,
    List<GatewayDeliveryMetrics> Gateways,
    List<DeliveryErrorFrequency> Errors);

public record ReconcileRequest(
    [Required] string GatewayId,
    [Required] int TargetVersion,
    [Required] string InitiatedBy
);

public record ReconcileResponse(
    string GatewayId,
    int TargetVersion,
    bool Success,
    string? ErrorMessage,
    DateTimeOffset ReconciledAt
);
