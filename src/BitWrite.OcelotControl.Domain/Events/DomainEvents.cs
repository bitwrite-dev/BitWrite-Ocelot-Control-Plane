using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;

namespace BitWrite.OcelotControl.Domain.Events;

public abstract record DomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public string CorrelationId { get; init; } = Guid.NewGuid().ToString();
    public string? CausationId { get; init; }
}

// Identity events
public record GatewayRegistered(GatewayId GatewayId, string Name, string? Description) : DomainEvent;
public record GatewayUpdated(GatewayId GatewayId, string? Name, string? Description) : DomainEvent;
public record GatewayStatusChanged(GatewayId GatewayId, string OldStatus, string NewStatus) : DomainEvent;

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
public record LicenseActivated(string LicenseId) : DomainEvent;
public record LicenseExpired(string LicenseId) : DomainEvent;
public record LicenseRevoked(string LicenseId, string Reason) : DomainEvent;

// Audit event
public record AuditRecorded(string Actor, string Action, string ResourceType, string ResourceId, string Result) : DomainEvent;

// Integration Events (§25.2)
public record SnapshotPublishedIntegrationEvent(SnapshotVersion Version, DateTimeOffset Timestamp) : DomainEvent;
public record SnapshotRolledBackIntegrationEvent(SnapshotVersion FromVersion, SnapshotVersion ToVersion, DateTimeOffset Timestamp) : DomainEvent;
public record RouteChangedIntegrationEvent(RouteId RouteId, string Action, DateTimeOffset Timestamp) : DomainEvent;
public record PluginLifecycleIntegrationEvent(PluginId PluginId, string State, DateTimeOffset Timestamp) : DomainEvent;
public record AuditIntegrationEvent(string AuditEntry) : DomainEvent;