using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Domain.ValueObjects.FeatureConfig;
using HttpMethod = BitWrite.OcelotControl.Domain.ValueObjects.Configuration.HttpMethod;

namespace BitWrite.OcelotControl.Domain.Services;

/// <summary>
/// Builds Ocelot configuration from management model (§11).
/// Transforms BitWrite management entities into Ocelot-compatible configuration.
/// </summary>
public class ConfigurationBuilder
{
    /// <summary>
    /// The Ocelot version the generated configuration targets.
    /// </summary>
    /// <remarks>
    /// Ocelot 18, the minimum supported version. Emitting the oldest shape is what
    /// lets one snapshot reach 18, 19 and 20. A later version that needs its own
    /// shape is a change here, and nowhere else.
    /// </remarks>
    public static OcelotVersion BaselineVersion => OcelotVersion.V18_0;

    private readonly ConfigurationCanonicalizer _canonicalizer;

    public ConfigurationBuilder(ConfigurationCanonicalizer canonicalizer)
    {
        _canonicalizer = canonicalizer;
    }

    /// <summary>
    /// Builds a complete Ocelot configuration from management entities.
    /// </summary>
    public OcelotConfiguration BuildConfiguration(
        IReadOnlyList<RouteConfiguration> routes,
        GlobalConfiguration globalConfig,
        OcelotVersion ocelotVersion)
    {
        var ocelotRoutes = new List<OcelotRouteConfiguration>();

        foreach (var route in routes)
        {
            var ocelotRoute = BuildRouteConfiguration(route, ocelotVersion);
            ocelotRoutes.Add(ocelotRoute);
        }

        var ocelotGlobalConfig = BuildGlobalConfiguration(globalConfig);

        return new OcelotConfiguration
        {
            Routes = ocelotRoutes,
            GlobalConfiguration = ocelotGlobalConfig
        };
    }

    /// <summary>
    /// Calculates a deterministic hash for the configuration.
    /// For identical management state and Ocelot version, the hash must be the same.
    /// </summary>
    public ConfigurationHash CalculateConfigurationHash(OcelotConfiguration configuration)
    {
        var canonical = _canonicalizer.Canonicalize(configuration);
        var bytes = Encoding.UTF8.GetBytes(canonical);
        var hashBytes = SHA256.HashData(bytes);
        return ConfigurationHash.FromBytes(hashBytes);
    }

    private OcelotRouteConfiguration BuildRouteConfiguration(RouteConfiguration route, OcelotVersion ocelotVersion)
    {
        var downstreamHostAndPorts = new List<OcelotHostAndPort>();
        OcelotLoadBalancerOptions? loadBalancerOptions = null;

        if (route.DownstreamTargets.Count == 1)
        {
            var target = route.DownstreamTargets[0];
            downstreamHostAndPorts.Add(new OcelotHostAndPort { Host = target.Host, Port = target.Port });
        }
        else if (route.DownstreamTargets.Count > 1)
        {
            loadBalancerOptions = new OcelotLoadBalancerOptions { Type = "RoundRobin" };
            downstreamHostAndPorts = route.DownstreamTargets
                .Select(t => new OcelotHostAndPort { Host = t.Host, Port = t.Port })
                .ToList();
        }

        var ocelotRoute = new OcelotRouteConfiguration
        {
            UpstreamPathTemplate = route.UpstreamPath.Value,
            UpstreamHttpMethod = new[] { route.Method.Value },
            // Collected on the route and previously never emitted, so an operator
            // could set a host and watch it not reach the gateway.
            UpstreamHost = route.Host,
            Priority = route.Priority,
            RouteIsCaseSensitive = route.RouteIsCaseSensitive,
            // Was hardcoded, so every route forwarded the upstream path and a
            // service under a different prefix could not be reached.
            DownstreamPathTemplate = route.DownstreamTemplate?.Value ?? "/{everything}",
            DownstreamScheme = route.DownstreamTargets.FirstOrDefault()?.Scheme ?? "http",
            DownstreamHostAndPorts = downstreamHostAndPorts,
            // The operator's own name, not the recomputed "Method:Path"
            // signature. The signature was published instead, so a route saved
            // as "header-transformation" reached the gateway as
            // "GET:/api/orders/{orderId}" and lost its name.
            Key = route.FriendlyKey,
            // The scopes a user configured used to be replaced with an empty
            // list here, so authentication was published with no scopes and the
            // gateway ignored it. They live in Properties["scopes"] as one
            // comma-separated value.
            AuthenticationOptions = route.AuthenticationOptions != null
                ? new OcelotAuthenticationOptions
                {
                    AllowedScopes = ParseScopes(route.AuthenticationOptions)
                }
                : null,
            RateLimitOptions = route.RateLimitOptions != null
                ? new OcelotRateLimitOptions
                {
                    EnableRateLimiting = true,
                    Period = route.RateLimitOptions.Period ?? "Minute",
                    Limit = route.RateLimitOptions.Limit ?? 100
                }
                : null,
            QoSOptions = route.QoSOptions != null
                ? new OcelotQoSOptions
                {
                    TimeoutValue = route.QoSOptions.TimeoutSeconds ?? 30,
                    DurationOfBreak = route.QoSOptions.CircuitBreakerTimeoutSeconds ?? 30
                }
                : null,
            FileCacheOptions = route.CacheOptions != null
                ? new OcelotFileCacheOptions { TtlSeconds = route.CacheOptions.TtlSeconds }
                : null,
            // Authorization and the transformation blocks. A value the target
            // version cannot express throws rather than being dropped, so a
            // rule an operator configured never silently fails to apply.
            RouteClaimsRequirement = FeatureOptionEmitter.Authorization(route.AuthorizationOptions),
            AddClaimsToRequest = FeatureOptionEmitter.AddClaimsToRequest(route.ClaimOptions),
            UpstreamHeaderTransform = FeatureOptionEmitter.UpstreamHeaderTransform(route.HeaderOptions),
            DownstreamHeaderTransform = FeatureOptionEmitter.DownstreamHeaderTransform(route.HeaderOptions),
            LoadBalancerOptions = loadBalancerOptions ?? (route.LoadBalancerOptions != null
                ? new OcelotLoadBalancerOptions { Type = route.LoadBalancerOptions.Algorithm }
                : null),
            // Transport and client behaviour. Ocelot's route-level `Timeout` is
            // separate from `QoSOptions.TimeoutValue`, which configures the
            // retry policy, so setting one never quietly changes the other.
            DownstreamHttpMethod = route.DownstreamMethod?.Value,
            DownstreamHttpVersion = route.DownstreamHttpVersion,
            DownstreamHttpVersionPolicy = route.DownstreamHttpVersionPolicy,
            DangerousAcceptAnyServerCertificateValidator =
                route.DangerousAcceptAnyServerCertificateValidator,
            DelegatingHandlers = route.DelegatingHandlers.Count > 0
                ? route.DelegatingHandlers.ToArray()
                : null,
            HttpHandlerOptions = route.HttpClientOptions != null
                ? new OcelotHttpHandlerOptions
                {
                    AllowAutoRedirect = route.HttpClientOptions.AllowAutoRedirect,
                    MaxConnectionsPerServer = route.HttpClientOptions.MaxConnectionsPerServer,
                    PooledConnectionLifetime = route.HttpClientOptions.PooledConnectionLifetimeSeconds,
                    UseCookieContainer = route.HttpClientOptions.UseCookieContainer,
                    UseProxy = route.HttpClientOptions.UseProxy,
                    UseTracing = route.HttpClientOptions.UseTracing
                }
                : null,
            Timeout = route.TimeoutSeconds
        };

        // Query transformation has no 18 counterpart, so this throws when one is
        // configured. Called last so the blocks that can be expressed are
        // validated first and the error names the right field.
        FeatureOptionEmitter.RejectQueryTransformations(route.QueryOptions);

        return ocelotRoute;
    }

    /// <summary>
    /// Reads the allowed scopes back out of the stored authentication options.
    /// </summary>
    /// <remarks>
    /// The options hold the scopes as a single comma-separated entry under
    /// "scopes", which is how the API layer stores them. Splitting happens here
    /// so the wire shape stays an Ocelot list.
    /// </remarks>
    private static List<string> ParseScopes(AuthenticationOptions options)
    {
        if (!options.Properties.TryGetValue("scopes", out var raw) || string.IsNullOrWhiteSpace(raw))
            return new List<string>();

        return raw
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(scope => scope.Trim())
            .Where(scope => scope.Length > 0)
            .ToList();
    }

    private OcelotGlobalConfiguration BuildGlobalConfiguration(GlobalConfiguration globalConfig)
    {
        return new OcelotGlobalConfiguration
        {
            BaseUrl = globalConfig.BaseUrl ?? "http://localhost:5000",
            RequestIdKey = globalConfig.RequestIdKey ?? "X-Request-Id"
        };
    }
}

public class RouteConfiguration
{
    public RouteId Id { get; init; } = default!;
    public RouteKey Key => RouteKey.Create(Method, UpstreamPath, Host);
    /// <summary>
    /// The name the operator gave the route, as distinct from the computed
    /// <see cref="Key"/> signature. Ocelot publishes this one, so a route saved
    /// as "orders" is recognisable in logs instead of appearing as
    /// "GET:/api/orders".
    /// </summary>
    public string? FriendlyKey { get; init; }
    public string? Host { get; init; }
    public HttpMethod Method { get; init; } = default!;
    public UpstreamPath UpstreamPath { get; init; } = default!;
    public ServiceId ServiceId { get; init; } = default!;
    public IReadOnlyList<DownstreamTarget> DownstreamTargets { get; init; } = Array.Empty<DownstreamTarget>();
    public AuthenticationOptions? AuthenticationOptions { get; init; }
    public AuthorizationOptions? AuthorizationOptions { get; init; }
    public RateLimitOptions? RateLimitOptions { get; init; }
    public QoSOptions? QoSOptions { get; init; }
    public CacheOptions? CacheOptions { get; init; }
    public LoadBalancerOptions? LoadBalancerOptions { get; init; }
    public HeaderOptions? HeaderOptions { get; init; }
    public ClaimOptions? ClaimOptions { get; init; }
    public QueryOptions? QueryOptions { get; init; }
    public int Priority { get; init; }
    public bool RouteIsCaseSensitive { get; init; }
    public DownstreamPathTemplate? DownstreamTemplate { get; init; }
    public HttpMethod? DownstreamMethod { get; init; }
    public string? DownstreamHttpVersion { get; init; }
    public string? DownstreamHttpVersionPolicy { get; init; }
    public bool DangerousAcceptAnyServerCertificateValidator { get; init; }
    public IReadOnlyList<string> DelegatingHandlers { get; init; } = Array.Empty<string>();
    public HttpClientOptions? HttpClientOptions { get; init; }
    public int? TimeoutSeconds { get; init; }
}

public class GlobalConfiguration
{
    public string? BaseUrl { get; init; }
    public string? RequestIdKey { get; init; }
}

public class OcelotConfiguration
{
    public List<OcelotRouteConfiguration> Routes { get; init; } = new();
    public OcelotGlobalConfiguration GlobalConfiguration { get; init; } = new();
}

public class OcelotRouteConfiguration
{
    public string UpstreamPathTemplate { get; init; } = string.Empty;
    public string[] UpstreamHttpMethod { get; init; } = Array.Empty<string>();
    public string? UpstreamHost { get; init; }
    public int Priority { get; init; }
    public bool RouteIsCaseSensitive { get; init; }
    /// <summary>
    /// A single verb, unlike <see cref="UpstreamHttpMethod"/> which is a list.
    /// </summary>
    public string? DownstreamHttpMethod { get; init; }

    public string? DownstreamHttpVersion { get; init; }

    public string? DownstreamHttpVersionPolicy { get; init; }

    public string DownstreamPathTemplate { get; init; } = string.Empty;
    public string DownstreamScheme { get; init; } = "http";
    public List<OcelotHostAndPort>? DownstreamHostAndPorts { get; init; }
    public string? Key { get; init; }
    public OcelotAuthenticationOptions? AuthenticationOptions { get; init; }
    public OcelotRateLimitOptions? RateLimitOptions { get; init; }
    public OcelotQoSOptions? QoSOptions { get; init; }
    public OcelotFileCacheOptions? FileCacheOptions { get; init; }
    public OcelotLoadBalancerOptions? LoadBalancerOptions { get; init; }
    public OcelotClaimsRequirement? RouteClaimsRequirement { get; init; }
    public Dictionary<string, string>? AddClaimsToRequest { get; init; }
    public Dictionary<string, string>? AddHeadersToRequest { get; init; }
    public Dictionary<string, string>? AddQueriesToRequest { get; init; }
    public Dictionary<string, string>? ChangeDownstreamPathTemplate { get; init; }
    public Dictionary<string, string>? UpstreamHeaderTransform { get; init; }
    public Dictionary<string, string>? DownstreamHeaderTransform { get; init; }
    public string[]? DelegatingHandlers { get; init; }
    public OcelotHttpHandlerOptions? HttpHandlerOptions { get; init; }
    public bool DangerousAcceptAnyServerCertificateValidator { get; init; }
    public int? Timeout { get; init; }
}

/// <summary>
/// Ocelot calls these <c>HttpHandlerOptions</c> even though they configure the
/// client that makes the downstream call.
/// </summary>
public class OcelotHttpHandlerOptions
{
    public bool AllowAutoRedirect { get; init; }
    public int MaxConnectionsPerServer { get; init; }
    public int PooledConnectionLifetime { get; init; }
    public bool UseCookieContainer { get; init; }
    public bool UseProxy { get; init; }
    public bool UseTracing { get; init; }
}

public class OcelotGlobalConfiguration
{
    public string BaseUrl { get; init; } = "http://localhost:5000";
    public string RequestIdKey { get; init; } = "X-Request-Id";
}

public class OcelotHostAndPort
{
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; }
}

public class OcelotAuthenticationOptions
{
    public List<string> AllowedScopes { get; init; } = new();
}

public class OcelotRateLimitOptions
{
    public bool EnableRateLimiting { get; init; }
    public string Period { get; init; } = "Minute";
    public int Limit { get; init; }
}

public class OcelotQoSOptions
{
    public int TimeoutValue { get; init; }
    public int DurationOfBreak { get; init; }
}

public class OcelotFileCacheOptions
{
    public int TtlSeconds { get; init; }
}

public class OcelotLoadBalancerOptions
{
    public string Type { get; init; } = "RoundRobin";
}

/// <summary>
/// Ocelot 18 route-level authorization, which is a flat set of claim requirements.
/// </summary>
/// <remarks>
/// This is the whole of authorization in 18. There is no policy concept at route
/// level and no scope, so \`AuthorizationOptions.Policies\` and \`.Scopes\` have
/// nowhere to go — see the emission guard.
/// </remarks>
public class OcelotClaimsRequirement
{
    public Dictionary<string, string> Claims { get; init; } = new();
}