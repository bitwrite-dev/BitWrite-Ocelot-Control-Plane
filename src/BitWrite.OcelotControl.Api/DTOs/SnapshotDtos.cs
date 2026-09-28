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
    DateTimeOffset? ArchivedAt,
    /// <summary>
    /// How many routes this snapshot carries, or null when its content could
    /// not be read. Zero means the document has no routes; null means nobody
    /// can tell, and the two must not look the same.
    /// </summary>
    int? RouteCount,
    /// <summary>How many services this snapshot carries, or null if unreadable.</summary>
    int? ServiceCount,
    /// <summary>Plugin versions published in the snapshot, for the spec's column.</summary>
    IReadOnlyList<string> PluginVersions,
    /// <summary>
    /// Per-rule validation results, so a list can show a verdict rather than
    /// a bare status.
    /// </summary>
    IReadOnlyList<SnapshotValidationResultResponse> ValidationResults
);

public record SnapshotValidationResultResponse(
    string Rule,
    bool IsValid,
    string? Message
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
