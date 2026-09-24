using System.ComponentModel.DataAnnotations;

namespace BitWrite.OcelotControl.SDK.Models.Responses;

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

public record CloneSnapshotResponse(
    int NewVersion,
    int ClonedFromVersion,
    string Name
);

public record ExportSnapshotResponse(
    int Version,
    string Content,
    string Format
);

public record SnapshotDeploymentResponse(
    string PublicationId,
    int SnapshotVersion,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    string? FailureReason
);