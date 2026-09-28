using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;

namespace BitWrite.OcelotControl.Application.UseCases.SystemSettings;

/// <summary>
/// The settings, plus what a first-run screen needs to render itself.
/// </summary>
public record SystemSettingsResponse(
    string Id,
    /// <summary>Null until a version is chosen, which is the first-run state.</summary>
    string? OcelotVersion,
    DateTimeOffset? OcelotVersionSelectedAt,
    string? OcelotVersionSelectedBy,
    int PollIntervalSeconds,
    int AuditLogRetentionDays,
    int SnapshotRetentionCount,
    /// <summary>True once a version has been chosen and the setup is complete.</summary>
    bool IsInitialised,
    /// <summary>
    /// Versions that can be chosen, newest first.
    /// </summary>
    /// <remarks>
    /// Derived from what the builder can actually emit, not from what Ocelot has
    /// released. Offering 19 or 20 here would promise a shape nobody has written
    /// down, and the failure would only appear at a gateway that cannot start.
    /// </remarks>
    IReadOnlyList<string> AvailableOcelotVersions,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);
