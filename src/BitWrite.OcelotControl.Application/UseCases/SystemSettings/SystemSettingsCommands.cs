namespace BitWrite.OcelotControl.Application.UseCases.SystemSettings;

/// <summary>
/// Reads the system settings, which is also how a client learns whether
/// first-run setup is still outstanding.
/// </summary>
public record GetSystemSettingsQuery(string InitiatedBy = "", string CorrelationId = "");

/// <summary>
/// Updates the operational settings.
/// </summary>
/// <remarks>
/// The Ocelot version is deliberately absent: it is chosen once, through
/// <see cref="CompleteFirstRunCommand"/>, and has no update path at all. Putting
/// it here would be the one field on this command with no meaningful value.
/// </remarks>
public record UpdateSystemSettingsCommand(
    int? PollIntervalSeconds = null,
    int? AuditLogRetentionDays = null,
    int? SnapshotRetentionCount = null,
    string InitiatedBy = "",
    string CorrelationId = ""
);

/// <summary>
/// The one-time first-run choice: the Ocelot version, alongside the rest of the
/// system configuration chosen with it.
/// </summary>
public record CompleteFirstRunCommand(
    /// <summary>Exactly one, and it must be a version whose shapes are established.</summary>
    string OcelotVersion,
    int? PollIntervalSeconds = null,
    int? AuditLogRetentionDays = null,
    int? SnapshotRetentionCount = null,
    string InitiatedBy = "",
    string CorrelationId = ""
);
