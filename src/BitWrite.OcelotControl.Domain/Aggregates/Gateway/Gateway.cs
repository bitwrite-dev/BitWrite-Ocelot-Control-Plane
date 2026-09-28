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
    /// Restores a gateway from storage, preserving its identity and timestamps.
    /// </summary>
    /// <remarks>
    /// Adapters must use this rather than <see cref="Register"/>. Register mints a
    /// new id and stamps fresh timestamps, so a read followed by a write wrote the
    /// row under a different key — and the next read of the original key found
    /// nothing, which surfaced as "gateway not found" on a save that had in fact
    /// appeared to succeed. Each save also left a duplicate row behind.
    /// <para>
    /// No event is raised: nothing happened, the row already existed.
    /// </para>
    /// </remarks>
    public static Gateway Reconstitute(
        GatewayId id,
        string name,
        string? description,
        RuntimeStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Gateway name cannot be empty", "INVALID_GATEWAY_NAME");

        return new Gateway
        {
            Id = id,
            Name = name.Trim(),
            Description = description,
            Status = status,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
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
    /// Updates gateway name.
    /// </summary>
    public void UpdateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Gateway name cannot be empty", "INVALID_GATEWAY_NAME");

        Name = name.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new GatewayUpdated(Id, Name, Description));
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
    /// Marks the gateway for removal.
    /// </summary>
    /// <remarks>
    /// Deletion is refused once a publication has been addressed to this
    /// gateway, because <c>Publication.GatewayStates</c> is keyed by
    /// <see cref="GatewayId"/> and the history would refer to a gateway that no
    /// longer exists. The check is on the caller because the aggregate cannot see
    /// publications.
    /// <para>
    /// The refusal lives here rather than in a use case so there is no other way
    /// to reach a deletion — this aggregate had no delete at all, and the
    /// repository method that would have removed one was never called.
    /// </para>
    /// </remarks>
    /// <param name="hasBeenPublishedTo">
    /// True when any publication has carried this gateway's id.
    /// </param>
    /// <exception cref="DomainException">
    /// When the gateway has already been a publication target.
    /// </exception>
    public void Delete(bool hasBeenPublishedTo, string correlationId = "")
    {
        if (hasBeenPublishedTo)
        {
            throw new DomainException(
                "A gateway that has been published to cannot be deleted, because publication history refers to it",
                "GATEWAY_HAS_PUBLICATION_HISTORY");
        }

        UpdatedAt = DateTimeOffset.UtcNow;
        AddDomainEvent(new GatewayDeleted(Id, Name));
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