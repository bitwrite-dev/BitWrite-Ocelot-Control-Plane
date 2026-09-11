using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Domain.Events;
using BitWrite.OcelotControl.Domain.Exceptions;

namespace BitWrite.OcelotControl.Domain.Aggregates.Service;

/// <summary>
/// Service Aggregate Root - Logical Downstream Service (§9A.2)
/// Represents a logical service with one or more host endpoints.
/// </summary>
public class Service
{
    private readonly List<DomainEvent> _domainEvents = new();
    private readonly List<ServiceEndpoint> _endpoints = new();

    public ServiceId Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyList<ServiceEndpoint> Endpoints => _endpoints.AsReadOnly();
    public IReadOnlyList<DomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    private Service() { }

    /// <summary>
    /// Factory method to create a new service.
    /// </summary>
    public static Service Create(string name, string? description = null, string correlationId = "")
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Service name cannot be empty", "INVALID_SERVICE_NAME");

        var service = new Service
        {
            Id = ServiceId.New(),
            Name = name.Trim(),
            Description = description?.Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        service.AddDomainEvent(new ServiceCreated(service.Id, service.Name));
        return service;
    }

    /// <summary>
    /// Adds a host endpoint to the service.
    /// </summary>
    public void AddHost(string host, int port, int weight = 1, string correlationId = "")
    {
        if (string.IsNullOrWhiteSpace(host))
            throw new DomainException("Host cannot be empty", "INVALID_HOST");

        if (port <= 0 || port > 65535)
            throw new DomainException($"Invalid port: {port}", "INVALID_PORT");

        if (weight <= 0)
            throw new DomainException("Weight must be positive", "INVALID_WEIGHT");

        if (_endpoints.Any(e => e.Host == host.Trim().ToLowerInvariant() && e.Port == port))
            throw new DomainException($"Endpoint {host}:{port} already exists", "DUPLICATE_ENDPOINT");

        var endpoint = new ServiceEndpoint
        {
            Host = host.Trim().ToLowerInvariant(),
            Port = port,
            Weight = weight,
            IsActive = true
        };

        _endpoints.Add(endpoint);
        UpdatedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new ServiceHostAdded(Id, endpoint.Host, endpoint.Port));
    }

    /// <summary>
    /// Removes a host endpoint from the service.
    /// </summary>
    public void RemoveHost(string host, int port, string correlationId = "")
    {
        var endpoint = _endpoints.FirstOrDefault(e =>
            e.Host == host.Trim().ToLowerInvariant() && e.Port == port);

        if (endpoint == null)
            throw new DomainException($"Endpoint {host}:{port} not found", "ENDPOINT_NOT_FOUND");

        if (_endpoints.Count <= 1)
            throw new DomainException("Service must have at least one endpoint", "NO_ENDPOINTS");

        _endpoints.Remove(endpoint);
        UpdatedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new ServiceHostRemoved(Id, host, port));
    }

    /// <summary>
    /// Updates the weight of a host endpoint.
    /// </summary>
    public void UpdateHostWeight(string host, int port, int weight)
    {
        if (weight <= 0)
            throw new DomainException("Weight must be positive", "INVALID_WEIGHT");

        var endpoint = _endpoints.FirstOrDefault(e =>
            e.Host == host.Trim().ToLowerInvariant() && e.Port == port);

        if (endpoint == null)
            throw new DomainException($"Endpoint {host}:{port} not found", "ENDPOINT_NOT_FOUND");

        endpoint.Weight = weight;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Enables or disables a host endpoint.
    /// </summary>
    public void SetHostActive(string host, int port, bool isActive)
    {
        var endpoint = _endpoints.FirstOrDefault(e =>
            e.Host == host.Trim().ToLowerInvariant() && e.Port == port);

        if (endpoint == null)
            throw new DomainException($"Endpoint {host}:{port} not found", "ENDPOINT_NOT_FOUND");

        endpoint.IsActive = isActive;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Updates the service name.
    /// </summary>
    public void UpdateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Service name cannot be empty", "INVALID_SERVICE_NAME");

        Name = name.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Updates the service description.
    /// </summary>
    public void UpdateDescription(string? description)
    {
        Description = description?.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Gets all active endpoints.
    /// </summary>
    public IReadOnlyList<ServiceEndpoint> GetActiveEndpoints()
    {
        return _endpoints.Where(e => e.IsActive).ToList().AsReadOnly();
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

/// <summary>
/// Service endpoint entity.
/// </summary>
public class ServiceEndpoint
{
    public string Host { get; internal set; } = string.Empty;
    public int Port { get; internal set; }
    public int Weight { get; internal set; } = 1;
    public bool IsActive { get; internal set; } = true;
}