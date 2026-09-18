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