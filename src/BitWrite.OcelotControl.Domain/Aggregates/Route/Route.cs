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
    /// Reconstitutes a route from persisted state, preserving its identity,
    /// enabled state and timestamps.
    ///
    /// Infrastructure adapters must use this rather than <see cref="Create"/>:
    /// Create mints a new <see cref="RouteId"/>, forces <c>IsEnabled</c> back to
    /// true and stamps <see cref="DateTimeOffset.UtcNow"/>, so reloading a route
    /// would change its id every time and would undo a disable.
    /// </summary>
    public static Route Reconstitute(
        RouteId id,
        HttpMethod method,
        UpstreamPath upstreamPath,
        ServiceId serviceId,
        IReadOnlyList<DownstreamTarget> downstreamTargets,
        bool isEnabled,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        string? key = null,
        string? host = null,
        AuthenticationOptions? authenticationOptions = null,
        AuthorizationOptions? authorizationOptions = null,
        RateLimitOptions? rateLimitOptions = null,
        QoSOptions? qosOptions = null,
        CacheOptions? cacheOptions = null,
        LoadBalancerOptions? loadBalancerOptions = null,
        HeaderOptions? headerOptions = null,
        ClaimOptions? claimOptions = null,
        QueryOptions? queryOptions = null)
    {
        if (downstreamTargets is null || downstreamTargets.Count == 0)
            throw new DomainException("Route must have at least one downstream target", "NO_DOWNSTREAM_TARGETS");

        return new Route
        {
            Id = id,
            Method = method,
            UpstreamPath = upstreamPath,
            ServiceId = serviceId,
            Key = key?.Trim(),
            Host = host?.ToLowerInvariant().Trim(),
            IsEnabled = isEnabled,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            // The feature configs used to be dropped here, so a route came back
            // from storage with none of them. They are assigned directly because
            // the setters stamp UpdatedAt, which would overwrite the stored
            // value this method is given.
            AuthenticationOptions = authenticationOptions,
            AuthorizationOptions = authorizationOptions,
            RateLimitOptions = rateLimitOptions,
            QoSOptions = qosOptions,
            CacheOptions = cacheOptions,
            LoadBalancerOptions = loadBalancerOptions,
            HeaderOptions = headerOptions,
            ClaimOptions = claimOptions,
            QueryOptions = queryOptions
        }.WithTargets(downstreamTargets);
    }

    /// <summary>
    /// Appends targets to a newly built route. Split out so Reconstitute does not
    /// repeat the assignment that Create performs.
    /// </summary>
    private Route WithTargets(IReadOnlyList<DownstreamTarget> targets)
    {
        _downstreamTargets.AddRange(targets);
        return this;
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

    /// <summary>
    /// Replaces the whole configuration of the route.
    /// </summary>
    /// <remarks>
    /// A replacement rather than a merge, so every field ends up holding exactly
    /// what the caller asked for. That is what makes it possible to clear a
    /// value: under merge semantics a null is indistinguishable from "leave it
    /// alone", so a host or a rate limit could be set but never removed.
    /// <para>
    /// Done as one operation so a validation failure leaves the route untouched,
    /// and so a single <see cref="RouteUpdated"/> event describes the change.
    /// </para>
    /// </remarks>
    public void Replace(
        HttpMethod method,
        UpstreamPath upstreamPath,
        ServiceId serviceId,
        IReadOnlyList<DownstreamTarget> downstreamTargets,
        string? key,
        string? host,
        AuthenticationOptions? authenticationOptions,
        RateLimitOptions? rateLimitOptions,
        QoSOptions? qosOptions,
        CacheOptions? cacheOptions,
        LoadBalancerOptions? loadBalancerOptions,
        AuthorizationOptions? authorizationOptions = null,
        HeaderOptions? headerOptions = null,
        ClaimOptions? claimOptions = null,
        QueryOptions? queryOptions = null)
    {
        if (downstreamTargets == null || downstreamTargets.Count == 0)
            throw new DomainException("Route must have at least one downstream target", "NO_DOWNSTREAM_TARGETS");

        if (downstreamTargets.Count > 10)
            throw new DomainException("Route cannot have more than 10 downstream targets", "TOO_MANY_DOWNSTREAM_TARGETS");

        if (downstreamTargets.Distinct().Count() != downstreamTargets.Count)
            throw new DomainException("Downstream targets must be unique", "DUPLICATE_DOWNSTREAM_TARGET");

        Method = method;
        UpstreamPath = upstreamPath;
        ServiceId = serviceId;
        _downstreamTargets.Clear();
        _downstreamTargets.AddRange(downstreamTargets);

        Key = key?.Trim();
        Host = host?.ToLowerInvariant().Trim();

        AuthenticationOptions = authenticationOptions;
        // A replacement is a replacement: an option block that is not supplied
        // is removed, which is how a feature gets switched off.
        AuthorizationOptions = authorizationOptions;
        RateLimitOptions = rateLimitOptions;
        QoSOptions = qosOptions;
        CacheOptions = cacheOptions;
        LoadBalancerOptions = loadBalancerOptions;
        HeaderOptions = headerOptions;
        ClaimOptions = claimOptions;
        QueryOptions = queryOptions;

        UpdatedAt = DateTimeOffset.UtcNow;
        AddDomainEvent(new RouteUpdated(Id));
    }

    /// <summary>
    /// Marks the route as deleted.
    /// </summary>
    public void Delete()
    {
        AddDomainEvent(new RouteDeleted(Id));
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