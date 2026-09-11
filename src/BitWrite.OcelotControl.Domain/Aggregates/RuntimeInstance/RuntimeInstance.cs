using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Domain.ValueObjects.Status;
using BitWrite.OcelotControl.Domain.Events;
using BitWrite.OcelotControl.Domain.Exceptions;

namespace BitWrite.OcelotControl.Domain.Aggregates.RuntimeInstance;

/// <summary>
/// RuntimeInstance Aggregate Root - Observed Gateway Runtime State (§9A.2, §9A.8)
/// Represents the actual runtime state of a gateway instance.
/// </summary>
public class RuntimeInstance
{
    private readonly List<DomainEvent> _domainEvents = new();
    private readonly List<string> _capabilities = new();
    private readonly List<string> _activeRoutes = new();

    public GatewayId GatewayId { get; private set; }
    public RuntimeStatus Status { get; private set; } = RuntimeStatus.Disconnected;
    public SnapshotVersion? CurrentVersion { get; private set; }
    public SnapshotVersion? TargetVersion { get; private set; }
    public DateTimeOffset LastHeartbeat { get; private set; }
    public DateTimeOffset? LastSynchronized { get; private set; }
    public DateTimeOffset? LastConfigApplied { get; private set; }
    public Dictionary<string, string> RuntimeInfo { get; private set; } = new();

    public IReadOnlyList<string> Capabilities => _capabilities.AsReadOnly();
    public IReadOnlyList<string> ActiveRoutes => _activeRoutes.AsReadOnly();
    public IReadOnlyList<DomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    private RuntimeInstance() { }

    /// <summary>
    /// Factory method to register a new runtime instance.
    /// </summary>
    public static RuntimeInstance Register(
        GatewayId gatewayId,
        IReadOnlyList<string> capabilities,
        string correlationId = "")
    {
        if (capabilities == null || capabilities.Count == 0)
            throw new DomainException("Runtime instance must have at least one capability", "NO_CAPABILITIES");

        var instance = new RuntimeInstance
        {
            GatewayId = gatewayId,
            Status = RuntimeStatus.Connecting,
            LastHeartbeat = DateTimeOffset.UtcNow
        };

        instance._capabilities.AddRange(capabilities);

        instance.AddDomainEvent(new RuntimeInstanceRegistered(gatewayId, string.Join(",", capabilities)));
        return instance;
    }

    /// <summary>
    /// Records a heartbeat from the runtime.
    /// </summary>
    public void RecordHeartbeat(string correlationId = "")
    {
        LastHeartbeat = DateTimeOffset.UtcNow;

        if (Status == RuntimeStatus.Disconnected)
        {
            Status = RuntimeStatus.Connecting;
        }

        AddDomainEvent(new RuntimeHeartbeatReceived(GatewayId));
    }

    /// <summary>
    /// Marks the instance as synchronized with the target version.
    /// </summary>
    public void MarkSynchronized(SnapshotVersion version, string correlationId = "")
    {
        Status = RuntimeStatus.Synchronized;
        CurrentVersion = version;
        LastSynchronized = DateTimeOffset.UtcNow;

        AddDomainEvent(new GatewayConfigurationApplied(GatewayId, version));
    }

    /// <summary>
    /// Marks the instance as actively applying configuration.
    /// </summary>
    public void MarkApplying(SnapshotVersion targetVersion)
    {
        Status = RuntimeStatus.Applying;
        TargetVersion = targetVersion;
    }

    /// <summary>
    /// Marks the instance as active (healthy).
    /// </summary>
    public void MarkActive()
    {
        Status = RuntimeStatus.Active;
    }

    /// <summary>
    /// Marks the instance as degraded.
    /// </summary>
    public void MarkDegraded(string reason)
    {
        Status = RuntimeStatus.Degraded;
        RuntimeInfo["DegradedReason"] = reason;
    }

    /// <summary>
    /// Marks the instance as disconnected.
    /// </summary>
    public void MarkDisconnected()
    {
        Status = RuntimeStatus.Disconnected;
        CurrentVersion = null;
        TargetVersion = null;
    }

    /// <summary>
    /// Records that configuration was applied.
    /// </summary>
    public void RecordConfigApplied(SnapshotVersion version, string correlationId = "")
    {
        CurrentVersion = version;
        TargetVersion = null;
        LastConfigApplied = DateTimeOffset.UtcNow;

        if (Status == RuntimeStatus.Applying)
        {
            Status = RuntimeStatus.Active;
        }

        AddDomainEvent(new GatewayConfigurationApplied(GatewayId, version));
    }

    /// <summary>
    /// Records a synchronization failure.
    /// </summary>
    public void RecordSyncFailure(string error, string correlationId = "")
    {
        RuntimeInfo["LastSyncError"] = error;
        RuntimeInfo["LastSyncErrorTime"] = DateTimeOffset.UtcNow.ToString("O");

        AddDomainEvent(new GatewaySynchronizationFailed(GatewayId, TargetVersion ?? CurrentVersion!, error));
    }

    /// <summary>
    /// Updates the active routes.
    /// </summary>
    public void UpdateActiveRoutes(IReadOnlyList<string> routes)
    {
        _activeRoutes.Clear();
        _activeRoutes.AddRange(routes);
    }

    /// <summary>
    /// Adds a runtime info entry.
    /// </summary>
    public void SetRuntimeInfo(string key, string value)
    {
        RuntimeInfo[key] = value;
    }

    /// <summary>
    /// Removes a runtime info entry.
    /// </summary>
    public void RemoveRuntimeInfo(string key)
    {
        RuntimeInfo.Remove(key);
    }

    /// <summary>
    /// Checks if the instance is healthy.
    /// </summary>
    public bool IsHealthy => Status.IsHealthy;

    /// <summary>
    /// Checks if the instance is degraded.
    /// </summary>
    public bool IsDegraded => Status.IsDegraded;

    /// <summary>
    /// Checks if the instance needs synchronization.
    /// </summary>
    public bool NeedsSynchronization =>
        TargetVersion != null &&
        CurrentVersion != null &&
        TargetVersion > CurrentVersion;

    /// <summary>
    /// Checks if the heartbeat is stale.
    /// </summary>
    public bool IsHeartbeatStale(TimeSpan threshold)
    {
        return DateTimeOffset.UtcNow - LastHeartbeat > threshold;
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