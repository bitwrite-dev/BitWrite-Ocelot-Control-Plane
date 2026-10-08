using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;

namespace BitWrite.OcelotControl.Domain.Events;

public abstract record DomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    /// <summary>
    /// Set by the aggregate from the correlation id it was given, so a trace can
    /// be followed from a request through the events it produced.
    /// </summary>
    /// <remarks>
    /// This was a plain initialiser, so it always produced a fresh guid and the
    /// value passed to the aggregate was discarded. An event declaring
    /// <c>CorrelationId</c> as a positional parameter therefore reported a random
    /// one — which reads as a working trace and is not.
    /// </remarks>
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString();
    public string? CausationId { get; init; }

}

/// <summary>
/// Helpers for the events above.
/// </summary>
public static class DomainEventExtensions
{
    /// <summary>
    /// Lands a correlation id on an event that was constructed without one.
    /// </summary>
    /// <remarks>
    /// A record's positional constructor cannot set an inherited <c>init</c>
    /// property, so an event declaring <c>CorrelationId</c> as a positional
    /// parameter would otherwise always report a fresh guid instead of the one
    /// the aggregate was given. The setter exists for that reason; prefer
    /// <c>init</c> wherever it is usable.
    /// </remarks>
    public static TEvent WithCorrelationId<TEvent>(this TEvent domainEvent, string? correlationId)
        where TEvent : DomainEvent
    {
        if (!string.IsNullOrWhiteSpace(correlationId))
            domainEvent.CorrelationId = correlationId;

        return domainEvent;
    }
}

// Identity events
public record GatewayRegistered(GatewayId GatewayId, string Name, string? Description) : DomainEvent;
public record GatewayUpdated(GatewayId GatewayId, string? Name, string? Description) : DomainEvent;
public record GatewayStatusChanged(GatewayId GatewayId, string OldStatus, string NewStatus) : DomainEvent;
public record GatewayDeleted(GatewayId GatewayId, string Name) : DomainEvent;
public record GatewayEnvironmentChanged(GatewayId GatewayId, string OldEnvironment, string NewEnvironment) : DomainEvent;

// Route events
public record RouteCreated(RouteId RouteId, RouteKey RouteKey, ServiceId ServiceId) : DomainEvent;
public record RouteUpdated(RouteId RouteId) : DomainEvent;
public record RouteDisabled(RouteId RouteId) : DomainEvent;
public record RouteEnabled(RouteId RouteId) : DomainEvent;
public record RouteDeleted(RouteId RouteId) : DomainEvent;

// Service events
public record ServiceCreated(ServiceId ServiceId, string Name) : DomainEvent;
public record ServiceUpdated(ServiceId ServiceId) : DomainEvent;
public record ServiceHostAdded(ServiceId ServiceId, string Host, int Port) : DomainEvent;
public record ServiceHostRemoved(ServiceId ServiceId, string Host, int Port) : DomainEvent;
public record ServiceDeleted(ServiceId ServiceId) : DomainEvent;

// GlobalConfiguration events
public record GlobalConfigurationUpdated() : DomainEvent;

// SystemSettings events
/// <summary>
/// The one-time choice of Ocelot version, and the end of first-run setup.
/// </summary>
public record SystemSettingsInitialised(
    OcelotVersion OcelotVersion,
    string SelectedBy,
    string CorrelationId) : DomainEvent;

// Snapshot events
public record SnapshotCreated(SnapshotVersion Version, ConfigurationHash Hash, string CreatedBy) : DomainEvent;
public record SnapshotValidated(SnapshotVersion Version, bool IsValid) : DomainEvent;
public record SnapshotPublished(SnapshotVersion Version) : DomainEvent;
public record SnapshotArchived(SnapshotVersion Version) : DomainEvent;
public record SnapshotRolledBack(SnapshotVersion FromVersion, SnapshotVersion ToVersion) : DomainEvent;
public record SnapshotValidationFailed(SnapshotVersion Version, string Reason) : DomainEvent;

// Publication events
public record PublicationStarted(PublicationId PublicationId, SnapshotVersion SnapshotVersion) : DomainEvent;
public record PublicationCompleted(PublicationId PublicationId) : DomainEvent;
public record PublicationFailed(PublicationId PublicationId, string Reason) : DomainEvent;
public record PublicationRolledBack(PublicationId PublicationId, SnapshotVersion TargetVersion) : DomainEvent;

// Gateway runtime events
public record GatewayConfigurationApplied(GatewayId GatewayId, SnapshotVersion Version) : DomainEvent;
public record GatewaySynchronizationFailed(GatewayId GatewayId, SnapshotVersion Version, string Error) : DomainEvent;
public record RuntimeInstanceRegistered(GatewayId GatewayId, string Capabilities) : DomainEvent;
public record RuntimeHeartbeatReceived(GatewayId GatewayId) : DomainEvent;

// Plugin events
public record PluginInstalled(PluginId PluginId, string Version) : DomainEvent;
public record PluginEnabled(PluginId PluginId) : DomainEvent;
public record PluginDisabled(PluginId PluginId) : DomainEvent;
public record PluginUpgraded(PluginId PluginId, string OldVersion, string NewVersion) : DomainEvent;
public record PluginUninstalled(PluginId PluginId) : DomainEvent;

// License events
public record LicenseCreated(LicenseId LicenseId, string Name, string ProductCode, DateTimeOffset ExpirationDate) : DomainEvent;
public record LicenseActivated(LicenseId LicenseId) : DomainEvent;
public record LicenseUpdated(LicenseId LicenseId) : DomainEvent;
public record LicenseExpired(LicenseId LicenseId) : DomainEvent;
public record LicenseRevoked(LicenseId LicenseId, string Reason) : DomainEvent;
public record LicenseRenewed(LicenseId LicenseId, DateTimeOffset NewExpirationDate) : DomainEvent;

// Audit event
public record AuditRecorded(string Actor, string Action, string ResourceType, string ResourceId, string Result) : DomainEvent;

// Integration Events (§25.2)
public record SnapshotPublishedIntegrationEvent(SnapshotVersion Version, DateTimeOffset Timestamp) : DomainEvent;
public record SnapshotRolledBackIntegrationEvent(SnapshotVersion FromVersion, SnapshotVersion ToVersion, DateTimeOffset Timestamp) : DomainEvent;
public record RouteChangedIntegrationEvent(RouteId RouteId, string Action, DateTimeOffset Timestamp) : DomainEvent;
public record PluginLifecycleIntegrationEvent(PluginId PluginId, string State, DateTimeOffset Timestamp) : DomainEvent;
public record AuditIntegrationEvent(string AuditEntry) : DomainEvent;
