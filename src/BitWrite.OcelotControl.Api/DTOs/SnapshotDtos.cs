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

/// <summary>
/// The artifact a snapshot would contain, resolved and validated but not stored.
/// </summary>
public record PreviewSnapshotResponse(
    /// <summary>
    /// The canonical artifact, or null when the management state could not be
    /// built into one. Null rather than an empty document, because an empty
    /// document would read as a valid artifact with nothing in it.
    /// </summary>
    string? Content,
    /// <summary>
    /// The hash this artifact would carry, computed over the exact string that
    /// would be stored, or null when there is nothing to hash.
    /// </summary>
    string? Hash,
    int RouteCount,
    int ServiceCount,
    IReadOnlyList<string> PluginVersions,
    IReadOnlyList<SnapshotValidationResultResponse> ValidationResults,
    bool IsValid,
    string OcelotVersion,
    /// <summary>
    /// The version this snapshot would take. Reported, not consumed: these
    /// numbers are what a rollback names, so a preview must not spend one.
    /// </summary>
    int NextVersion
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

/// <summary>
/// The environment a publication is for.
/// </summary>
/// <remarks>
/// Required, and not defaulted. A publication has to name the environment it serves
/// because routes, services and snapshots are stored per environment, and answering
/// a request that names none would publish one environment's snapshot to another
/// environment's gateways without anyone saying so.
/// </remarks>
public record SnapshotPublishRequest(
    [Required] string InitiatedBy,
    [Required] string Environment,
    [Required] int[] TargetGatewayIds
);

/// <summary>
/// The environment a rollback is for.
/// </summary>
/// <remarks>
/// Required, and not defaulted. A rollback has to name the environment it serves
/// because routes, services and snapshots are stored per environment, and answering
/// a request that names none would roll back one environment's gateways while
/// speaking to another's.
/// </remarks>
public record SnapshotRollbackRequest(
    [Required] string InitiatedBy,
    [Required] string Environment,
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
