using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Domain.ValueObjects.Status;
using BitWrite.OcelotControl.Domain.Events;
using BitWrite.OcelotControl.Domain.Exceptions;

namespace BitWrite.OcelotControl.Domain.Aggregates.Gateway;

/// <summary>
/// Gateway Aggregate Root - Identity & Management Association (§9A.2)
/// Represents a registered Ocelot gateway instance.
/// </summary>
public class Gateway
{
    private readonly List<DomainEvent> _domainEvents = new();

    public GatewayId Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public RuntimeStatus Status { get; private set; } = RuntimeStatus.Disconnected;
    public Dictionary<string, string> Metadata { get; private set; } = new();
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyList<DomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    private Gateway() { }

    /// <summary>
    /// Factory method to register a new gateway.
    /// </summary>
    public static Gateway Register(string name, string? description = null, string correlationId = "")
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Gateway name cannot be empty", "INVALID_GATEWAY_NAME");

        var gateway = new Gateway
        {
            Id = GatewayId.New(),
            Name = name.Trim(),
            Description = description?.Trim(),
            Status = RuntimeStatus.Disconnected,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        gateway.AddDomainEvent(new GatewayRegistered(gateway.Id, gateway.Name, gateway.Description));
        return gateway;
    }

    /// <summary>
    /// Updates gateway metadata.
    /// </summary>
    public void UpdateMetadata(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new DomainException("Metadata key cannot be empty", "INVALID_METADATA_KEY");

        Metadata[key.Trim()] = value;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Removes metadata entry.
    /// </summary>
    public void RemoveMetadata(string key)
    {
        if (Metadata.Remove(key))
        {
            UpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    /// <summary>
    /// Sets gateway status.
    /// </summary>
    public void SetStatus(RuntimeStatus newStatus, string correlationId = "")
    {
        if (Status == newStatus)
            return;

        var oldStatus = Status.Value;
        Status = newStatus;
        UpdatedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new GatewayStatusChanged(Id, oldStatus, newStatus.Value));
    }

    /// <summary>
    /// Updates gateway description.
    /// </summary>
    public void UpdateDescription(string? description)
    {
        Description = description?.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Checks if gateway is healthy.
    /// </summary>
    public bool IsHealthy => Status.IsHealthy;

    /// <summary>
    /// Checks if gateway is degraded.
    /// </summary>
    public bool IsDegraded => Status.IsDegraded;

    private void AddDomainEvent(DomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}