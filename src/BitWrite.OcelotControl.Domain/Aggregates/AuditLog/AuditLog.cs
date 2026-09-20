using BitWrite.OcelotControl.Domain.Events;

namespace BitWrite.OcelotControl.Domain.Aggregates.AuditLog;

/// <summary>
/// AuditLog Entity - Audit Context (§9A.3)
/// Records all significant actions performed in the system for compliance and debugging.
/// </summary>
public class AuditLog
{
    private readonly List<DomainEvent> _domainEvents = new();

    public string Id { get; private set; } = string.Empty;
    public string Actor { get; private set; } = string.Empty;
    public string Action { get; private set; } = string.Empty;
    public string ResourceType { get; private set; } = string.Empty;
    public string ResourceId { get; private set; } = string.Empty;
    public string Result { get; private set; } = string.Empty;
    public DateTimeOffset Timestamp { get; private set; }
    public string? CorrelationId { get; private set; }
    public string? Details { get; private set; }

    public IReadOnlyList<DomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    private AuditLog() { }

    /// <summary>
    /// Factory method to create a new audit log entry.
    /// </summary>
    public static AuditLog Create(
        string actor,
        string action,
        string resourceType,
        string resourceId,
        string result,
        string? correlationId = null,
        string? details = null)
    {
        if (string.IsNullOrWhiteSpace(actor))
            throw new Domain.Exceptions.DomainException("Actor cannot be empty", "INVALID_AUDIT_ACTOR");

        if (string.IsNullOrWhiteSpace(action))
            throw new Domain.Exceptions.DomainException("Action cannot be empty", "INVALID_AUDIT_ACTION");

        if (string.IsNullOrWhiteSpace(resourceType))
            throw new Domain.Exceptions.DomainException("Resource type cannot be empty", "INVALID_AUDIT_RESOURCE_TYPE");

        if (string.IsNullOrWhiteSpace(resourceId))
            throw new Domain.Exceptions.DomainException("Resource ID cannot be empty", "INVALID_AUDIT_RESOURCE_ID");

        var auditLog = new AuditLog
        {
            Id = GenerateId(),
            Actor = actor.Trim(),
            Action = action.Trim(),
            ResourceType = resourceType.Trim(),
            ResourceId = resourceId.Trim(),
            Result = result.Trim(),
            Timestamp = DateTimeOffset.UtcNow,
            CorrelationId = correlationId,
            Details = details
        };

        auditLog.AddDomainEvent(new AuditRecorded(
            actor,
            action,
            resourceType,
            resourceId,
            result));

        return auditLog;
    }

    /// <summary>
    /// Creates an audit log from a domain event.
    /// </summary>
    public static AuditLog FromDomainEvent(DomainEvent domainEvent, string result)
    {
        var eventType = domainEvent.GetType().Name;
        var resourceId = ExtractResourceId(domainEvent);

        return Create(
            "System",
            eventType,
            ExtractResourceType(domainEvent),
            resourceId,
            result,
            domainEvent.CorrelationId);
    }

    private static string ExtractResourceId(DomainEvent domainEvent)
    {
        return domainEvent switch
        {
            Events.LicenseCreated e => e.LicenseId.Value.ToString(),
            Events.LicenseActivated e => e.LicenseId.Value.ToString(),
            Events.LicenseUpdated e => e.LicenseId.Value.ToString(),
            Events.LicenseExpired e => e.LicenseId.Value.ToString(),
            Events.LicenseRevoked e => e.LicenseId.Value.ToString(),
            Events.LicenseRenewed e => e.LicenseId.Value.ToString(),
            Events.RouteCreated e => e.RouteId.Value.ToString(),
            Events.RouteUpdated e => e.RouteId.Value.ToString(),
            Events.RouteDisabled e => e.RouteId.Value.ToString(),
            Events.RouteEnabled e => e.RouteId.Value.ToString(),
            Events.RouteDeleted e => e.RouteId.Value.ToString(),
            Events.ServiceCreated e => e.ServiceId.Value.ToString(),
            Events.ServiceUpdated e => e.ServiceId.Value.ToString(),
            Events.ServiceDeleted e => e.ServiceId.Value.ToString(),
            Events.ServiceHostAdded e => $"{e.ServiceId.Value}:{e.Host}:{e.Port}",
            Events.ServiceHostRemoved e => $"{e.ServiceId.Value}:{e.Host}:{e.Port}",
            Events.GatewayRegistered e => e.GatewayId.Value.ToString(),
            Events.GatewayUpdated e => e.GatewayId.Value.ToString(),
            Events.GatewayStatusChanged e => e.GatewayId.Value.ToString(),
            Events.GatewayConfigurationApplied e => e.GatewayId.Value.ToString(),
            Events.GatewaySynchronizationFailed e => e.GatewayId.Value.ToString(),
            Events.GlobalConfigurationUpdated => "GlobalConfig",
            Events.SnapshotCreated e => e.Version.Value.ToString(),
            Events.SnapshotValidated e => e.Version.Value.ToString(),
            Events.SnapshotArchived e => e.Version.Value.ToString(),
            Events.SnapshotRolledBack e => e.ToVersion.Value.ToString(),
            Events.SnapshotValidationFailed e => e.Version.Value.ToString(),
            Events.PublicationStarted e => e.PublicationId.Value.ToString(),
            Events.PublicationCompleted e => e.PublicationId.Value.ToString(),
            Events.PublicationFailed e => e.PublicationId.Value.ToString(),
            Events.PublicationRolledBack e => e.PublicationId.Value.ToString(),
            Events.PluginInstalled e => e.PluginId.Value,
            Events.PluginEnabled e => e.PluginId.Value,
            Events.PluginDisabled e => e.PluginId.Value,
            Events.PluginUpgraded e => e.PluginId.Value,
            Events.PluginUninstalled e => e.PluginId.Value,
            Events.RuntimeInstanceRegistered e => e.GatewayId.Value.ToString(),
            Events.RuntimeHeartbeatReceived e => e.GatewayId.Value.ToString(),
            _ => "Unknown"
        };
    }

    private static string ExtractResourceType(DomainEvent domainEvent)
    {
        return domainEvent switch
        {
            Events.LicenseCreated or Events.LicenseActivated or Events.LicenseUpdated
                or Events.LicenseExpired or Events.LicenseRevoked or Events.LicenseRenewed => "License",
            Events.RouteCreated or Events.RouteUpdated or Events.RouteDisabled
                or Events.RouteEnabled or Events.RouteDeleted => "Route",
            Events.ServiceCreated or Events.ServiceUpdated or Events.ServiceDeleted
                or Events.ServiceHostAdded or Events.ServiceHostRemoved => "Service",
            Events.GatewayRegistered or Events.GatewayUpdated or Events.GatewayStatusChanged
                or Events.GatewayConfigurationApplied or Events.GatewaySynchronizationFailed => "Gateway",
            Events.GlobalConfigurationUpdated => "GlobalConfiguration",
            Events.SnapshotCreated or Events.SnapshotValidated or Events.SnapshotArchived
                or Events.SnapshotRolledBack or Events.SnapshotValidationFailed => "Snapshot",
            Events.PublicationStarted or Events.PublicationCompleted
                or Events.PublicationFailed or Events.PublicationRolledBack => "Publication",
            Events.PluginInstalled or Events.PluginEnabled or Events.PluginDisabled
                or Events.PluginUpgraded or Events.PluginUninstalled => "Plugin",
            Events.RuntimeInstanceRegistered or Events.RuntimeHeartbeatReceived => "Runtime",
            _ => "Unknown"
        };
    }

    private static string GenerateId()
    {
        return $"audit_{Guid.NewGuid():N}";
    }

    private void AddDomainEvent(DomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}
