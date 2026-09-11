using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.FeatureConfig;
using BitWrite.OcelotControl.Domain.Events;
using BitWrite.OcelotControl.Domain.Exceptions;
using HttpMethod = BitWrite.OcelotControl.Domain.ValueObjects.Configuration.HttpMethod;

namespace BitWrite.OcelotControl.Domain.Aggregates.Route;

/// <summary>
/// Route Aggregate Root - Central Configuration Aggregate (§9A.2)
/// Represents a routing rule in the Ocelot configuration.
/// </summary>
public class Route
{
    private readonly List<DomainEvent> _domainEvents = new();
    private readonly List<DownstreamTarget> _downstreamTargets = new();

    public RouteId Id { get; private set; }
    public string? Key { get; private set; }
    public string? Host { get; private set; }
    public HttpMethod Method { get; private set; } = default!;
    public UpstreamPath UpstreamPath { get; private set; } = default!;
    public ServiceId ServiceId { get; private set; } = default!;
    public bool IsEnabled { get; private set; } = true;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    // Feature configurations
    public AuthenticationOptions? AuthenticationOptions { get; private set; }
    public AuthorizationOptions? AuthorizationOptions { get; private set; }
    public RateLimitOptions? RateLimitOptions { get; private set; }
    public QoSOptions? QoSOptions { get; private set; }
    public CacheOptions? CacheOptions { get; private set; }
    public LoadBalancerOptions? LoadBalancerOptions { get; private set; }
    public HeaderOptions? HeaderOptions { get; private set; }
    public ClaimOptions? ClaimOptions { get; private set; }
    public QueryOptions? QueryOptions { get; private set; }

    public IReadOnlyList<DownstreamTarget> DownstreamTargets => _downstreamTargets.AsReadOnly();
    public IReadOnlyList<DomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public RouteKey RouteKey => RouteKey.Create(Method, UpstreamPath, Host);

    private Route() { }

    /// <summary>
    /// Factory method to create a new route.
    /// </summary>
    public static Route Create(
        HttpMethod method,
        UpstreamPath upstreamPath,
        ServiceId serviceId,
        IReadOnlyList<DownstreamTarget> downstreamTargets,
        string? key = null,
        string? host = null,
        string correlationId = "")
    {
        if (downstreamTargets == null || downstreamTargets.Count == 0)
            throw new DomainException("Route must have at least one downstream target", "NO_DOWNSTREAM_TARGETS");

        if (downstreamTargets.Count > 10)
            throw new DomainException("Route cannot have more than 10 downstream targets", "TOO_MANY_DOWNSTREAM_TARGETS");

        var route = new Route
        {
            Id = RouteId.New(),
            Method = method,
            UpstreamPath = upstreamPath,
            ServiceId = serviceId,
            Key = key?.Trim(),
            Host = host?.ToLowerInvariant().Trim(),
            IsEnabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        route._downstreamTargets.AddRange(downstreamTargets);

        route.AddDomainEvent(new RouteCreated(route.Id, route.RouteKey, route.ServiceId));
        return route;
    }

    /// <summary>
    /// Enables the route.
    /// </summary>
    public void Enable(string correlationId = "")
    {
        if (IsEnabled)
            return;

        IsEnabled = true;
        UpdatedAt = DateTimeOffset.UtcNow;
        AddDomainEvent(new RouteEnabled(Id));
    }

    /// <summary>
    /// Disables the route.
    /// </summary>
    public void Disable(string correlationId = "")
    {
        if (!IsEnabled)
            return;

        IsEnabled = false;
        UpdatedAt = DateTimeOffset.UtcNow;
        AddDomainEvent(new RouteDisabled(Id));
    }

    /// <summary>
    /// Sets authentication options.
    /// </summary>
    public void SetAuthentication(AuthenticationOptions? options)
    {
        AuthenticationOptions = options;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Sets authorization options.
    /// </summary>
    public void SetAuthorization(AuthorizationOptions? options)
    {
        AuthorizationOptions = options;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Sets rate limit options.
    /// </summary>
    public void SetRateLimit(RateLimitOptions? options)
    {
        RateLimitOptions = options;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Sets QoS options.
    /// </summary>
    public void SetQoS(QoSOptions? options)
    {
        QoSOptions = options;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Sets cache options.
    /// </summary>
    public void SetCache(CacheOptions? options)
    {
        CacheOptions = options;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Sets load balancer options.
    /// </summary>
    public void SetLoadBalancer(LoadBalancerOptions? options)
    {
        LoadBalancerOptions = options;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Sets header transformation options.
    /// </summary>
    public void SetHeaders(HeaderOptions? options)
    {
        HeaderOptions = options;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Sets claim transformation options.
    /// </summary>
    public void SetClaims(ClaimOptions? options)
    {
        ClaimOptions = options;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Sets query string transformation options.
    /// </summary>
    public void SetQuery(QueryOptions? options)
    {
        QueryOptions = options;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Adds a downstream target.
    /// </summary>
    public void AddDownstreamTarget(DownstreamTarget target)
    {
        if (_downstreamTargets.Count >= 10)
            throw new DomainException("Route cannot have more than 10 downstream targets", "TOO_MANY_DOWNSTREAM_TARGETS");

        if (_downstreamTargets.Any(t => t == target))
            throw new DomainException("Downstream target already exists", "DUPLICATE_DOWNSTREAM_TARGET");

        _downstreamTargets.Add(target);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Removes a downstream target.
    /// </summary>
    public void RemoveDownstreamTarget(DownstreamTarget target)
    {
        if (_downstreamTargets.Count <= 1)
            throw new DomainException("Route must have at least one downstream target", "NO_DOWNSTREAM_TARGETS");

        if (_downstreamTargets.Remove(target))
        {
            UpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    /// <summary>
    /// Updates the route key.
    /// </summary>
    public void UpdateKey(string? key)
    {
        Key = key?.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Updates the host.
    /// </summary>
    public void UpdateHost(string? host)
    {
        Host = host?.ToLowerInvariant().Trim();
        UpdatedAt = DateTimeOffset.UtcNow;
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