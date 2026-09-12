namespace BitWrite.OcelotControl.Api.DTOs;

public record PublicationResponse(
    string Id,
    int SnapshotVersion,
    string Status,
    string InitiatedBy,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    string? FailureReason,
    List<GatewayDeploymentStateResponse> GatewayStates
);

public record GatewayDeploymentStateResponse(
    string GatewayId,
    string Status,
    DateTimeOffset? ReceivedAt,
    DateTimeOffset? ValidatedAt,
    DateTimeOffset? AppliedAt,
    DateTimeOffset? HealthyAt,
    bool? IsValid,
    DateTimeOffset? FailedAt,
    string? FailureReason
);

public record PublicationListResponse(
    List<PublicationResponse> Publications,
    int TotalCount,
    int Page,
    int PageSize
);

public record CurrentPublicationResponse(
    PublicationResponse? Current,
    List<PublicationResponse> History
);