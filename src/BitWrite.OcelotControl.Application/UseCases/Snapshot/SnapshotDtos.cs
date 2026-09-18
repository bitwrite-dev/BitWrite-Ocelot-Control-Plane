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