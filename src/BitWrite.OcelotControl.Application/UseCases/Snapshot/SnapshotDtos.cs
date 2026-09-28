using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Domain.ValueObjects.Status;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;

namespace BitWrite.OcelotControl.Application.UseCases.Snapshot;

public record SnapshotResponse(
    SnapshotVersion Version,
    ConfigurationHash Hash,
    string Content,
    SnapshotStatus Status,
    string CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? ArchivedAt,
    IReadOnlyList<ValidationResult> ValidationResults
);

public record ValidationResult(
    string Rule,
    bool IsValid,
    string? Message
);

/// <summary>
/// What a snapshot would contain, resolved but not stored.
/// </summary>
public record PreviewSnapshotResponse(
    /// <summary>
    /// The canonical artifact, or null when the state could not be built into
    /// one. Null is reported rather than an empty document, because an empty
    /// document would look like a valid artifact with nothing in it.
    /// </summary>
    string? Content,
    /// <summary>
    /// The hash this artifact would carry, computed over the exact string that
    /// would be stored, or null when there is no content to hash.
    /// </summary>
    string? Hash,
    SnapshotCompositionSummary Composition,
    IReadOnlyList<ValidationResult> ValidationResults,
    bool IsValid,
    string OcelotVersion,
    /// <summary>
    /// The version this snapshot would take. Reported, not consumed: the numbers
    /// are what a rollback names, so a preview must not spend one.
    /// </summary>
    int NextVersion
);

/// <summary>
/// How many routes and services the artifact carries, and which plugin versions
/// it pins.
/// </summary>
public record SnapshotCompositionSummary(
    int RouteCount,
    int ServiceCount,
    IReadOnlyList<string> PluginVersions
);

public record SnapshotListResponse(
    IReadOnlyList<SnapshotResponse> Snapshots,
    int TotalCount,
    int Page,
    int PageSize
);

public record ValidateSnapshotResponse(
    bool IsValid,
    IReadOnlyList<string> Errors
);

public record CompareSnapshotsQuery(
    SnapshotVersion VersionA,
    SnapshotVersion VersionB
);

public record CompareSnapshotsResponse(
    SnapshotVersion VersionA,
    SnapshotVersion VersionB,
    IReadOnlyList<string> Differences
);

public record CloneSnapshotCommand(
    SnapshotVersion Version,
    string NewName,
    string InitiatedBy,
    string CorrelationId = ""
);

public record CloneSnapshotResponse(
    SnapshotVersion NewVersion,
    SnapshotVersion ClonedFromVersion,
    string Name
);

public record ExportSnapshotQuery(
    SnapshotVersion Version
);

public record ExportSnapshotResponse(
    SnapshotVersion Version,
    string Content,
    string Format
);

public record GetSnapshotDeploymentQuery(
    SnapshotVersion Version
);

public record SnapshotDeploymentResponse(
    string PublicationId,
    SnapshotVersion SnapshotVersion,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    string? FailureReason,
    IReadOnlyList<GatewayDeploymentState> GatewayStates
);

public record GatewayDeploymentState(
    GatewayId GatewayId,
    string GatewayName,
    string Status,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? FailureReason
);