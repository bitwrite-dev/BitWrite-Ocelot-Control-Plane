using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Domain.ValueObjects.Status;

namespace BitWrite.OcelotControl.Application.UseCases.Publication;

public record PublicationListResponse(
    IReadOnlyList<PublicationResponse> Publications,
    int TotalCount,
    int Page,
    int PageSize
);

public record CurrentPublicationResponse(
    PublicationResponse? Current,
    IReadOnlyList<PublicationResponse> History
);

public record PublicationResponse(
    PublicationId Id,
    SnapshotVersion SnapshotVersion,
    PublicationStatus Status,
    string InitiatedBy,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    string? FailureReason,
    double ProgressPercentage,
    IReadOnlyList<GatewayDeploymentStateResponse> GatewayStates
);

public record GatewayDeploymentStateResponse(
    GatewayId GatewayId,
    string Status,
    DateTimeOffset? ReceivedAt,
    DateTimeOffset? ValidatedAt,
    DateTimeOffset? AppliedAt,
    DateTimeOffset? HealthyAt,
    bool? IsValid,
    DateTimeOffset? FailedAt,
    string? FailureReason
);