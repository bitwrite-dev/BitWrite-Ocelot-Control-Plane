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

    /// <summary>
    /// Evaluation order among routes that overlap; higher is matched first.
    /// </summary>
    /// <remarks>
    /// Ocelot orders by file order when this is absent, which is not something
    /// an operator can reason about once routes overlap.
    /// </remarks>
    public int Priority { get; private set; }

    /// <summary>
    /// Whether the upstream path and host match case-sensitively.
    /// </summary>
    /// <remarks>
    /// Absent means insensitive, so /api/Users and /api/users are the same route
    /// and one silently shadows the other.
    /// </remarks>
    public bool RouteIsCaseSensitive { get; private set; }

    /// <summary>
    /// The path the request is rewritten to on the way downstream.
    /// </summary>
    /// <remarks>
    /// Null forwards the upstream path unchanged, which is Ocelot's
    /// <c>/{everything}</c>. Setting it is what allows a service to live under a
    /// different prefix than the route the caller uses.
    /// </remarks>
    public DownstreamPathTemplate? DownstreamTemplate { get; private set; }

    /// <summary>
    /// The verb the request is rewritten to on the way downstream.
    /// </summary>
    /// <remarks>
    /// Null keeps the upstream verb. Ocelot models this as a single string
    /// rather than a list, unlike <c>UpstreamHttpMethod</c> which is a list.
    /// </remarks>
    public HttpMethod? DownstreamMethod { get; private set; }

    /// <summary>
    /// The HTTP version used for the downstream request.
    /// </summary>
    /// <remarks>
    /// Ocelot accepts "1.0", "1.1" or "2.0". Null leaves the framework's
    /// default in place, which is not the same as asking for 1.1.
    /// </remarks>
    public string? DownstreamHttpVersion { get; private set; }

    /// <summary>
    /// How strictly the version is requested: exact, or at least / at most.
    /// </summary>
    /// <remarks>
    /// Without a policy, asking for 2.0 over plain HTTP negotiates down to 1.1
    /// and the gateway logs a protocol error. The two settings are only useful
    /// together.
    /// </remarks>
    public string? DownstreamHttpVersionPolicy { get; private set; }

    /// <summary>
    /// Accepts any TLS certificate from the downstream service.
    /// </summary>
    /// <remarks>
    /// For self-signed certificates in local development only. Ocelot's own
    /// documentation calls it a security risk on 20.0 and later, and the route
    /// schema says so in its own comment.
    /// </remarks>
    public bool DangerousAcceptAnyServerCertificateValidator { get; private set; }

    /// <summary>
    /// Names of Ocelot delegating handlers to run for this route.
    /// </summary>
    /// <remarks>
    /// The handlers are registered in the gateway, not here. A name that is not
    /// registered makes the gateway fail to start, so the list cannot be
    /// validated from the control plane alone.
    /// </remarks>
    public IReadOnlyList<string> DelegatingHandlers => _delegatingHandlers.AsReadOnly();
    private readonly List<string> _delegatingHandlers = new();

    /// <summary>
    /// How the gateway's HTTP client behaves when calling downstream.
    /// </summary>
    public HttpClientOptions? HttpClientOptions { get; private set; }

    /// <summary>
    /// Seconds the gateway waits for a downstream response, or null for the
    /// framework default.
    /// </summary>
    /// <remarks>
    /// A zero or negative value is treated as "no timeout" by Ocelot, which is
    /// a good way to forget about it, so the aggregate rejects it.
    /// </remarks>
    public int? TimeoutSeconds { get; private set; }

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
        string correlationId = "",
        DownstreamPathTemplate? downstreamTemplate = null,
        HttpMethod? downstreamMethod = null,
        string? downstreamHttpVersion = null,
        string? downstreamHttpVersionPolicy = null,
        bool acceptAnyServerCertificate = false,
        IReadOnlyList<string>? delegatingHandlers = null,
        HttpClientOptions? httpClientOptions = null,
        int? timeoutSeconds = null)
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

        route.SetDownstreamTemplate(downstreamTemplate);
        route.SetDownstreamMethod(downstreamMethod);
        route.SetDownstreamHttpVersion(downstreamHttpVersion, downstreamHttpVersionPolicy);
        route.SetAcceptAnyServerCertificate(acceptAnyServerCertificate);
        route.SetDelegatingHandlers(delegatingHandlers);
        route.SetHttpClientOptions(httpClientOptions);
        route.SetTimeout(timeoutSeconds);

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
        int priority = 0,
        bool routeIsCaseSensitive = false,
        DownstreamPathTemplate? downstreamTemplate = null,
        HttpMethod? downstreamMethod = null,
        string? downstreamHttpVersion = null,
        string? downstreamHttpVersionPolicy = null,
        bool acceptAnyServerCertificate = false,
        IReadOnlyList<string>? delegatingHandlers = null,
        HttpClientOptions? httpClientOptions = null,
        int? timeoutSeconds = null,
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

        var instance = new Route
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
            Priority = priority,
            RouteIsCaseSensitive = routeIsCaseSensitive,
            DownstreamTemplate = downstreamTemplate,
            DownstreamMethod = downstreamMethod,
            DownstreamHttpVersion = downstreamHttpVersion,
            DownstreamHttpVersionPolicy = downstreamHttpVersionPolicy,
            DangerousAcceptAnyServerCertificateValidator = acceptAnyServerCertificate,
            HttpClientOptions = httpClientOptions,
            TimeoutSeconds = timeoutSeconds,
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

        if (delegatingHandlers != null)
        {
            foreach (var handler in delegatingHandlers)
            {
                instance._delegatingHandlers.Add(handler);
            }
        }

        return instance;
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
        QueryOptions? queryOptions = null,
        int priority = 0,
        bool routeIsCaseSensitive = false,
        DownstreamPathTemplate? downstreamTemplate = null,
        HttpMethod? downstreamMethod = null,
        string? downstreamHttpVersion = null,
        string? downstreamHttpVersionPolicy = null,
        bool acceptAnyServerCertificate = false,
        IReadOnlyList<string>? delegatingHandlers = null,
        HttpClientOptions? httpClientOptions = null,
        int? timeoutSeconds = null)
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
        Priority = priority;
        RouteIsCaseSensitive = routeIsCaseSensitive;

        // Applied through the setters so the same validation runs, then folded
        // into the single event this operation raises.
        SetDownstreamTemplate(downstreamTemplate);
        SetDownstreamMethod(downstreamMethod);
        SetDownstreamHttpVersion(downstreamHttpVersion, downstreamHttpVersionPolicy);
        SetAcceptAnyServerCertificate(acceptAnyServerCertificate);
        SetDelegatingHandlers(delegatingHandlers);
        SetHttpClientOptions(httpClientOptions);
        SetTimeout(timeoutSeconds);

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

    /// <summary>
    /// Sets the path the request is rewritten to downstream.
    /// </summary>
    /// <remarks>
    /// A placeholder the upstream path cannot fill arrives empty at the
    /// service, so it is refused here rather than discovered as a puzzling 404
    /// later. <c>{everything}</c> is always available because it is Ocelot's own
    /// catch-all.
    /// </remarks>
    public void SetDownstreamTemplate(DownstreamPathTemplate? template, string correlationId = "")
    {
        if (template != null)
        {
            var available = UpstreamPath.Placeholders()
                .Concat(new[] { "everything" })
                .ToHashSet(StringComparer.Ordinal);

            var unknown = template.Placeholders()
                .Where(name => !available.Contains(name))
                .ToList();

            if (unknown.Count > 0)
            {
                throw new DomainException(
                    $"Downstream path uses placeholders the upstream path cannot fill: {string.Join(", ", unknown.Select(n => "{" + n + "}"))}",
                    "DOWNSTREAM_PLACEHOLDER_NOT_IN_UPSTREAM");
            }
        }

        DownstreamTemplate = template;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Sets the verb the request is rewritten to downstream.
    /// </summary>
    public void SetDownstreamMethod(HttpMethod? method, string correlationId = "")
    {
        DownstreamMethod = method;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Sets the downstream HTTP version and how strictly it is requested.
    /// </summary>
    /// <remarks>
    /// The two are validated together. Ocelot accepts "1.0", "1.1" and "2.0",
    /// and the policy has to be one of its three named values — an unknown one
    /// would bind to nothing and the gateway would quietly fall back.
    /// </remarks>
    public void SetDownstreamHttpVersion(
        string? version,
        string? policy = null,
        string correlationId = "")
    {
        var validVersions = new[] { "1.0", "1.1", "2.0" };
        var validPolicies = new[] { "RequestVersionExact", "RequestVersionOrHigher", "RequestVersionOrLower" };

        if (version is not null && !validVersions.Contains(version))
        {
            throw new DomainException(
                $"Invalid downstream HTTP version: {version}. Valid: {string.Join(", ", validVersions)}",
                "INVALID_HTTP_VERSION");
        }

        if (policy is not null && !validPolicies.Contains(policy))
        {
            throw new DomainException(
                $"Invalid HTTP version policy: {policy}. Valid: {string.Join(", ", validPolicies)}",
                "INVALID_HTTP_VERSION_POLICY");
        }

        if (policy is not null && version is null)
        {
            // A policy with nothing to apply it to would read as configured
            // while changing nothing.
            throw new DomainException(
                "An HTTP version policy needs a version to apply to",
                "HTTP_VERSION_POLICY_WITHOUT_VERSION");
        }

        DownstreamHttpVersion = version;
        DownstreamHttpVersionPolicy = policy;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Whether the downstream certificate is accepted without validation.
    /// </summary>
    public void SetAcceptAnyServerCertificate(bool accept, string correlationId = "")
    {
        DangerousAcceptAnyServerCertificateValidator = accept;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Replaces the delegating handlers run for this route.
    /// </summary>
    public void SetDelegatingHandlers(IReadOnlyList<string>? handlers, string correlationId = "")
    {
        _delegatingHandlers.Clear();
        if (handlers != null)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var handler in handlers)
            {
                if (string.IsNullOrWhiteSpace(handler))
                    throw new DomainException("A delegating handler name cannot be empty", "INVALID_DELEGATING_HANDLER");

                var name = handler.Trim();
                if (!seen.Add(name))
                {
                    // Ocelot would register the same handler twice.
                    throw new DomainException($"Delegating handler '{name}' is listed twice", "DUPLICATE_DELEGATING_HANDLER");
                }

                _delegatingHandlers.Add(name);
            }
        }

        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetHttpClientOptions(HttpClientOptions? options, string correlationId = "")
    {
        HttpClientOptions = options;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Sets how long the gateway waits for a downstream response.
    /// </summary>
    public void SetTimeout(int? seconds, string correlationId = "")
    {
        if (seconds is <= 0)
        {
            // Ocelot reads zero or less as "no timeout", which is a good way to
            // end up waiting forever.
            throw new DomainException("Timeout must be positive", "INVALID_TIMEOUT");
        }

        TimeoutSeconds = seconds;
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