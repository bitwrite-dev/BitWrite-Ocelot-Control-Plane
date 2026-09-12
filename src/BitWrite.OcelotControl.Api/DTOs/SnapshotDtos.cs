using System.ComponentModel.DataAnnotations;

namespace BitWrite.OcelotControl.Api.DTOs;

public record CreateSnapshotRequest(
    [Required] string InitiatedBy,
    string? CorrelationId
);

public record SnapshotResponse(
    int Version,
    string Hash,
    string Content,
    string Status,
    string CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? ArchivedAt
);

public record SnapshotListResponse(
    List<SnapshotResponse> Snapshots,
    int TotalCount,
    int Page,
    int PageSize
);

public record SnapshotValidationResponse(
    bool IsValid,
    List<string> Errors
);

public record SnapshotCompareResponse(
    int VersionA,
    int VersionB,
    List<string> Differences
);

public record SnapshotPublishRequest(
    [Required] string InitiatedBy,
    List<string>? TargetGatewayIds
);

public record SnapshotRollbackRequest(
    [Required] string InitiatedBy,
    [Required] int TargetVersion,
    [Required] string Reason
);

public record SnapshotDeploymentResponse(
    string PublicationId,
    int SnapshotVersion,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    string? FailureReason
);

public record SnapshotCloneRequest(
    [Required] string InitiatedBy,
    [Required] string NewName
);