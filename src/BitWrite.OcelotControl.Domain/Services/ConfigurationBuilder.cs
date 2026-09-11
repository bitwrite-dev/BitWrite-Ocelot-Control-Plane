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
            DownstreamPathTemplate = "/{everything}",
            DownstreamScheme = route.DownstreamTargets.FirstOrDefault()?.Scheme ?? "http",
            DownstreamHostAndPorts = downstreamHostAndPorts,
            Key = route.Key?.ToSignature(),
            AuthenticationOptions = route.AuthenticationOptions != null
                ? new OcelotAuthenticationOptions { AllowedScopes = new List<string>() }
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
            LoadBalancerOptions = loadBalancerOptions ?? (route.LoadBalancerOptions != null
                ? new OcelotLoadBalancerOptions { Type = route.LoadBalancerOptions.Algorithm }
                : null)
        };

        return ocelotRoute;
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
    public string? Host { get; init; }
    public HttpMethod Method { get; init; } = default!;
    public UpstreamPath UpstreamPath { get; init; } = default!;
    public ServiceId ServiceId { get; init; } = default!;
    public IReadOnlyList<DownstreamTarget> DownstreamTargets { get; init; } = Array.Empty<DownstreamTarget>();
    public AuthenticationOptions? AuthenticationOptions { get; init; }
    public RateLimitOptions? RateLimitOptions { get; init; }
    public QoSOptions? QoSOptions { get; init; }
    public CacheOptions? CacheOptions { get; init; }
    public LoadBalancerOptions? LoadBalancerOptions { get; init; }
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
    public string DownstreamPathTemplate { get; init; } = string.Empty;
    public string DownstreamScheme { get; init; } = "http";
    public List<OcelotHostAndPort>? DownstreamHostAndPorts { get; init; }
    public string? Key { get; init; }
    public OcelotAuthenticationOptions? AuthenticationOptions { get; init; }
    public OcelotRateLimitOptions? RateLimitOptions { get; init; }
    public OcelotQoSOptions? QoSOptions { get; init; }
    public OcelotFileCacheOptions? FileCacheOptions { get; init; }
    public OcelotLoadBalancerOptions? LoadBalancerOptions { get; init; }
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