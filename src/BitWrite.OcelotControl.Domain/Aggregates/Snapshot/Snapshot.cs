using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.Status;
using BitWrite.OcelotControl.Domain.Events;
using BitWrite.OcelotControl.Domain.Exceptions;

namespace BitWrite.OcelotControl.Domain.Aggregates.Snapshot;

/// <summary>
/// Snapshot Aggregate Root - Immutable Release Artifact (§9A.2)
/// Represents an immutable, complete, validated runtime configuration artifact.
/// </summary>
public class Snapshot
{
    private readonly List<DomainEvent> _domainEvents = new();
    private readonly List<ValidationResult> _validationResults = new();

    public SnapshotVersion Version { get; private set; }
    public SnapshotStatus Status { get; private set; } = SnapshotStatus.Ready;
    public ConfigurationHash Hash { get; private set; } = default!;
    public string Content { get; private set; } = string.Empty;
    public string CreatedBy { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }
    public DateTimeOffset? ArchivedAt { get; private set; }

    public IReadOnlyList<ValidationResult> ValidationResults => _validationResults.AsReadOnly();
    public IReadOnlyList<DomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    private Snapshot() { }

    /// <summary>
    /// Factory method to create a new snapshot.
    /// </summary>
    public static Snapshot Create(
        string content,
        ConfigurationHash hash,
        SnapshotVersion version,
        string createdBy,
        IReadOnlyList<ValidationResult>? validationResults = null,
        string correlationId = "")
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new DomainException("Snapshot content cannot be empty", "EMPTY_SNAPSHOT_CONTENT");

        if (string.IsNullOrWhiteSpace(createdBy))
            throw new DomainException("Created by cannot be empty", "INVALID_CREATED_BY");

        var snapshot = new Snapshot
        {
            Version = version,
            Status = SnapshotStatus.Ready,
            Hash = hash,
            Content = content,
            CreatedBy = createdBy.Trim(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        if (validationResults != null)
        {
            snapshot._validationResults.AddRange(validationResults);
        }

        snapshot.AddDomainEvent(new SnapshotCreated(version, hash, createdBy));
        return snapshot;
    }

    /// <summary>
    /// Reconstitutes a snapshot from persisted state, preserving status and
    /// timestamps. Unlike <see cref="Create"/>, this raises no domain events,
    /// because the events it would represent have already happened.
    /// </summary>
    /// <param name="validationResults">
    /// The rules that were run when this snapshot was sealed, restored as they were
    /// recorded.
    /// </param>
    /// <remarks>
    /// Optional because snapshots stored before this field existed have none, and
    /// reading one has to keep working. An older document restores with an empty
    /// list rather than failing, which the page shows as "not validated" — which
    /// is the truth about a snapshot nobody recorded a check for.
    /// </remarks>
    public static Snapshot Reconstitute(
        string content,
        ConfigurationHash hash,
        SnapshotVersion version,
        SnapshotStatus status,
        string createdBy,
        DateTimeOffset createdAt,
        DateTimeOffset? publishedAt = null,
        DateTimeOffset? archivedAt = null,
        IReadOnlyList<ValidationResult>? validationResults = null)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new DomainException("Snapshot content cannot be empty", "EMPTY_SNAPSHOT_CONTENT");

        if (string.IsNullOrWhiteSpace(createdBy))
            throw new DomainException("Created by cannot be empty", "INVALID_CREATED_BY");

        var instance = new Snapshot
        {
            Version = version,
            Status = status,
            Hash = hash,
            Content = content,
            CreatedBy = createdBy.Trim(),
            CreatedAt = createdAt,
            PublishedAt = publishedAt,
            ArchivedAt = archivedAt
        };

        if (validationResults is not null)
        {
            // Restored in order, because the sequence is the report: the first
            // failure explains the rest.
            foreach (var result in validationResults)
            {
                instance._validationResults.Add(result);
            }
        }

        return instance;
    }

    /// <summary>
    /// Marks the snapshot as validated.
    /// </summary>
    public void MarkValidated(bool isValid, string correlationId = "")
    {
        if (Status != SnapshotStatus.Ready)
            throw new DomainException($"Cannot validate snapshot in {Status} status", "INVALID_SNAPSHOT_STATUS");

        AddDomainEvent(new SnapshotValidated(Version, isValid));
    }

    /// <summary>
    /// Publishes the snapshot.
    /// </summary>
    public void Publish(string correlationId = "")
    {
        if (!Status.CanTransitionTo(SnapshotStatus.Published))
            throw new DomainException($"Cannot publish snapshot in {Status} status", "INVALID_SNAPSHOT_STATUS");

        Status = SnapshotStatus.Published;
        PublishedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new SnapshotPublished(Version));
    }

    /// <summary>
    /// Archives the snapshot.
    /// </summary>
    public void Archive(string correlationId = "")
    {
        if (!Status.CanTransitionTo(SnapshotStatus.Archived))
            throw new DomainException($"Cannot archive snapshot in {Status} status", "INVALID_SNAPSHOT_STATUS");

        Status = SnapshotStatus.Archived;
        ArchivedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new SnapshotArchived(Version));
    }

    /// <summary>
    /// Marks the snapshot as rolled back to.
    /// </summary>
    public void Rollback(SnapshotVersion fromVersion, string correlationId = "")
    {
        AddDomainEvent(new SnapshotRolledBack(fromVersion, Version));
    }

    /// <summary>
    /// Verifies snapshot integrity by recomputing hash.
    /// </summary>
    public bool VerifyIntegrity(ConfigurationHash computedHash)
    {
        return Hash == computedHash;
    }

    /// <summary>
    /// Adds a validation result.
    /// </summary>
    public void AddValidationResult(ValidationResult result)
    {
        _validationResults.Add(result);
    }

    /// <summary>
    /// Gets all validation errors.
    /// </summary>
    public IReadOnlyList<ValidationResult> GetValidationErrors()
    {
        return _validationResults.Where(r => !r.IsValid).ToList().AsReadOnly();
    }

    /// <summary>
    /// Whether every rule that was run passed.
    /// </summary>
    /// <remarks>
    /// False when nothing was run. <c>All</c> over an empty list is vacuously true,
    /// which said "valid" about a snapshot whose validation nobody had recorded —
    /// while the API, given that same empty list, rendered "not validated". The two
    /// were describing one snapshot and disagreeing, and an operator reading the
    /// aggregate directly would have been told it was fine.
    /// <para>
    /// A warning counts here. The only rule that reports one is
    /// <c>HasContent</c>, and it says the snapshot would publish an empty
    /// configuration — worth saying, not worth refusing.
    /// </para>
    /// </remarks>
    public bool IsValid =>
        _validationResults.Count > 0 &&
        _validationResults.All(result => result.IsValid || IsWarning(result.Message));

    private void AddDomainEvent(DomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }

    private static bool IsWarning(string? message) =>
        message is not null && message.StartsWith("Warning", StringComparison.Ordinal);
}

/// <summary>
/// Validation result for snapshot.
/// </summary>
public class ValidationResult
{
    public string Rule { get; init; } = string.Empty;
    public bool IsValid { get; init; }
    public string? Message { get; init; }
}