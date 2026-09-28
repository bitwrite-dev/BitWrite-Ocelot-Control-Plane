using BitWrite.OcelotControl.Domain.Events;
using BitWrite.OcelotControl.Domain.Exceptions;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;

namespace BitWrite.OcelotControl.Domain.Aggregates.SystemSettings;

/// <summary>
/// System-wide settings owned by the control plane — the surface #447 called
/// for, and the one #484 puts the Ocelot version on.
/// </summary>
/// <remarks>
/// A singleton: one of these for the installation, not one per gateway. It is
/// deliberately <em>not</em> an extension of <c>GlobalConfiguration</c>, which is
/// a separate concept with its own page and endpoints, and which describes what
/// is published to gateways rather than how the control plane itself behaves.
/// <para>
/// Every gateway runs the chosen Ocelot version. The reason is that a single
/// <c>ocelot.json</c> has to satisfy every gateway that receives it, so a
/// per-gateway version would make one artifact unable to serve both.
/// </para>
/// </remarks>
public class SystemSettings
{
    private readonly List<DomainEvent> _domainEvents = new();

    /// <summary>Fixed: there is exactly one, so the id is not generated.</summary>
    public Guid Id { get; private set; } = SystemSettingsId.Value;

    /// <summary>
    /// The Ocelot version the generated configuration targets, or null before
    /// it has been chosen.
    /// </summary>
    /// <remarks>
    /// Null means the installation has not been set up yet, which is the state
    /// the first-run screen exists for. It is not a default of 18: silently
    /// assuming a version is the thing this whole issue exists to prevent.
    /// </remarks>
    public OcelotVersion? OcelotVersion { get; private set; }

    /// <summary>When the version was chosen, or null if it has not been.</summary>
    public DateTimeOffset? OcelotVersionSelectedAt { get; private set; }

    /// <summary>Who chose it, for the audit trail.</summary>
    public string? OcelotVersionSelectedBy { get; private set; }

    /// <summary>How often a gateway is asked for its configuration, in seconds.</summary>
    public int PollIntervalSeconds { get; private set; } = 30;

    /// <summary>How long an audit log entry is kept, in days. Zero keeps them forever.</summary>
    public int AuditLogRetentionDays { get; private set; } = 90;

    /// <summary>How many snapshots are kept. Zero keeps them all.</summary>
    public int SnapshotRetentionCount { get; private set; } = 0;

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyList<DomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    private SystemSettings() { }

    public static SystemSettings Create(string initiatedBy = "", string correlationId = "")
    {
        var now = DateTimeOffset.UtcNow;
        return new SystemSettings
        {
            Id = SystemSettingsId.Value,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>
    /// Restores settings from storage, including the chosen version.
    /// </summary>
    /// <remarks>
    /// Adapters must use this rather than <see cref="Create"/>, which mints a
    /// fresh timestamp pair and drops the version. A read followed by a save
    /// would then silently reset the choice.
    /// </remarks>
    public static SystemSettings Reconstitute(
        OcelotVersion? ocelotVersion,
        DateTimeOffset? ocelotVersionSelectedAt,
        string? ocelotVersionSelectedBy,
        int pollIntervalSeconds,
        int auditLogRetentionDays,
        int snapshotRetentionCount,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        return new SystemSettings
        {
            Id = SystemSettingsId.Value,
            OcelotVersion = ocelotVersion,
            OcelotVersionSelectedAt = ocelotVersionSelectedAt,
            OcelotVersionSelectedBy = ocelotVersionSelectedBy,
            PollIntervalSeconds = pollIntervalSeconds,
            AuditLogRetentionDays = auditLogRetentionDays,
            SnapshotRetentionCount = snapshotRetentionCount,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
    }

    /// <summary>Whether the version has been chosen yet, which is the first-run test.</summary>
    public bool IsInitialised => OcelotVersion is not null;

    /// <summary>
    /// Chooses the Ocelot version, once.
    /// </summary>
    /// <remarks>
    /// There is no change-after-selection path, and that is the decision rather
    /// than an omission. The version determines the shape of the generated
    /// <c>ocelot.json</c>, and every published snapshot is immutable and hashed.
    /// Changing it under existing data would leave every snapshot either wrong
    /// for the new version or orphaned, and a route that cannot be expressed in
    /// the target version is a dead end rather than a warning.
    /// <para>
    /// This mirrors what already stops a published snapshot from being edited: a
    /// change would only be possible by publishing a new snapshot, which means
    /// re-validating every route against the new version's shapes first.
    /// </para>
    /// </remarks>
    public void ChooseOcelotVersion(OcelotVersion version, string initiatedBy = "", string correlationId = "")
    {
        if (version is null)
            throw new DomainException("An Ocelot version must be chosen", "MISSING_OCELOT_VERSION");

        if (OcelotVersion is not null)
        {
            throw new DomainException(
                $"The Ocelot version was already chosen as {OcelotVersion} on " +
                $"{OcelotVersionSelectedAt:yyyy-MM-dd} and cannot be changed. Publishing a new " +
                "snapshot would not help: every route would have to be re-validated against " +
                "the target version's shapes first, and a route that cannot be expressed in it " +
                "is a dead end rather than a warning.",
                "OCELOT_VERSION_ALREADY_CHOSEN");
        }

        // Only offer a version whose shapes the builder can actually emit. Until
        // 19 and 20 are established, offering them would mean promising a shape
        // nobody has written down.
        if (!OcelotVersionCatalog.IsEmittable(version))
        {
            throw new DomainException(
                $"Ocelot {version} cannot be targeted yet: its configuration shapes are not " +
                "established. " + string.Join(", ", OcelotVersionCatalog.EmittableVersions().Select(v => v.ToString())) +
                " can.",
                "OCELOT_VERSION_NOT_EMITTABLE");
        }

        OcelotVersion = version;
        OcelotVersionSelectedAt = DateTimeOffset.UtcNow;
        OcelotVersionSelectedBy = string.IsNullOrWhiteSpace(initiatedBy) ? null : initiatedBy.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;

        // WithCorrelationId is what actually lands the id on the event: a record's
        // positional constructor cannot set an inherited init property.
        _domainEvents.Add(
            new SystemSettingsInitialised(version, OcelotVersionSelectedBy ?? "system", correlationId)
                .WithCorrelationId(correlationId));
    }

    public void SetPollInterval(int seconds, string correlationId = "")
    {
        if (seconds < 5)
        {
            // Below this the gateway spends its time asking rather than serving.
            throw new DomainException(
                "The poll interval must be at least 5 seconds", "INVALID_POLL_INTERVAL");
        }

        if (seconds > 3600)
            throw new DomainException("The poll interval must be an hour or less", "INVALID_POLL_INTERVAL");

        PollIntervalSeconds = seconds;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetAuditLogRetention(int days, string correlationId = "")
    {
        // Zero means keep forever, so it is a valid value rather than a missing one.
        if (days < 0 || days > 3650)
            throw new DomainException(
                "Audit log retention must be 0 (forever) or between 1 and 3650 days",
                "INVALID_AUDIT_RETENTION");

        AuditLogRetentionDays = days;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetSnapshotRetention(int count, string correlationId = "")
    {
        if (count < 0)
            throw new DomainException(
                "Snapshot retention must be 0 (keep all) or a positive count",
                "INVALID_SNAPSHOT_RETENTION");

        SnapshotRetentionCount = count;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Marks the events as dispatched, so a re-read of the same instance does not
    /// publish them a second time.
    /// </summary>
    public void ClearDomainEvents() => _domainEvents.Clear();

    private void AddDomainEvent(DomainEvent domainEvent) => _domainEvents.Add(domainEvent);
}

/// <summary>
/// The id of the one and only settings row, so a singleton needs no generated key.
/// </summary>
public static class SystemSettingsId
{
    public static Guid Value { get; } = new("00000000-0000-0000-0000-0000000000c0");
}
