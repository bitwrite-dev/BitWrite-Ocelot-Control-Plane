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