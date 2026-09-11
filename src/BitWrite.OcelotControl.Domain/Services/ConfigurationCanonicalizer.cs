using System.Text;
using System.Text.Json;

namespace BitWrite.OcelotControl.Domain.Services;

/// <summary>
/// Creates canonical representation for configuration hashing (§45.2).
/// Ensures that identical management state and Ocelot version produce the same canonical hash.
/// </summary>
public class ConfigurationCanonicalizer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Creates a canonical string representation of the configuration.
    /// The canonical form is deterministic and order-independent where appropriate.
    /// </summary>
    public string Canonicalize(OcelotConfiguration configuration)
    {
        var sb = new StringBuilder();

        // Canonicalize global configuration
        sb.AppendLine("GLOBAL:");
        sb.AppendLine($"  BaseUrl: {configuration.GlobalConfiguration.BaseUrl ?? "http://localhost:5000"}");
        sb.AppendLine($"  RequestIdKey: {configuration.GlobalConfiguration.RequestIdKey ?? "X-Request-Id"}");

        // Canonicalize routes (sorted by upstream path for determinism)
        var sortedRoutes = configuration.Routes
            .OrderBy(r => r.UpstreamPathTemplate)
            .ThenBy(r => string.Join(",", r.UpstreamHttpMethod.OrderBy(m => m)))
            .ToList();

        sb.AppendLine("ROUTES:");
        foreach (var route in sortedRoutes)
        {
            sb.AppendLine(CanonicalizeRoute(route));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Creates a JSON-based canonical representation.
    /// </summary>
    public string CanonicalizeJson(OcelotConfiguration configuration)
    {
        var canonical = new
        {
            global = new
            {
                baseUrl = configuration.GlobalConfiguration.BaseUrl ?? "http://localhost:5000",
                requestIdKey = configuration.GlobalConfiguration.RequestIdKey ?? "X-Request-Id"
            },
            routes = configuration.Routes
                .OrderBy(r => r.UpstreamPathTemplate)
                .ThenBy(r => string.Join(",", r.UpstreamHttpMethod.OrderBy(m => m)))
                .Select(r => CanonicalizeRouteForJson(r))
                .ToList()
        };

        return JsonSerializer.Serialize(canonical, JsonOptions);
    }

    private string CanonicalizeRoute(OcelotRouteConfiguration route)
    {
        var sb = new StringBuilder();
        sb.AppendLine("  ROUTE:");
        sb.AppendLine($"    UpstreamPathTemplate: {route.UpstreamPathTemplate}");
        sb.AppendLine($"    UpstreamHttpMethod: [{string.Join(",", route.UpstreamHttpMethod.OrderBy(m => m))}]");
        sb.AppendLine($"    DownstreamPathTemplate: {route.DownstreamPathTemplate}");
        sb.AppendLine($"    DownstreamScheme: {route.DownstreamScheme}");

        if (route.DownstreamHostAndPorts?.Any() == true)
        {
            sb.AppendLine("    DownstreamHostAndPorts:");
            foreach (var host in route.DownstreamHostAndPorts.OrderBy(h => h.Host).ThenBy(h => h.Port))
            {
                sb.AppendLine($"      - Host: {host.Host}, Port: {host.Port}");
            }
        }

        if (!string.IsNullOrEmpty(route.Key))
        {
            sb.AppendLine($"    Key: {route.Key}");
        }

        if (route.AuthenticationOptions != null)
        {
            sb.AppendLine($"    AuthenticationOptions: AllowedScopes=[{string.Join(",", route.AuthenticationOptions.AllowedScopes.OrderBy(s => s))}]");
        }

        if (route.RateLimitOptions != null)
        {
            sb.AppendLine($"    RateLimitOptions: EnableRateLimiting={route.RateLimitOptions.EnableRateLimiting}, Period={route.RateLimitOptions.Period}, Limit={route.RateLimitOptions.Limit}");
        }

        if (route.QoSOptions != null)
        {
            sb.AppendLine($"    QoSOptions: TimeoutValue={route.QoSOptions.TimeoutValue}, DurationOfBreak={route.QoSOptions.DurationOfBreak}");
        }

        if (route.FileCacheOptions != null)
        {
            sb.AppendLine($"    FileCacheOptions: TtlSeconds={route.FileCacheOptions.TtlSeconds}");
        }

        if (route.LoadBalancerOptions != null)
        {
            sb.AppendLine($"    LoadBalancerOptions: Type={route.LoadBalancerOptions.Type}");
        }

        return sb.ToString();
    }

    private object CanonicalizeRouteForJson(OcelotRouteConfiguration route)
    {
        return new
        {
            upstreamPathTemplate = route.UpstreamPathTemplate,
            upstreamHttpMethod = route.UpstreamHttpMethod.OrderBy(m => m).ToArray(),
            downstreamPathTemplate = route.DownstreamPathTemplate,
            downstreamScheme = route.DownstreamScheme,
            downstreamHostAndPorts = route.DownstreamHostAndPorts?
                .OrderBy(h => h.Host)
                .ThenBy(h => h.Port)
                .Select(h => new { host = h.Host, port = h.Port })
                .ToArray(),
            key = route.Key,
            authenticationOptions = route.AuthenticationOptions != null
                ? new { allowedScopes = route.AuthenticationOptions.AllowedScopes.OrderBy(s => s).ToArray() }
                : null,
            rateLimitOptions = route.RateLimitOptions != null
                ? new
                {
                    enableRateLimiting = route.RateLimitOptions.EnableRateLimiting,
                    period = route.RateLimitOptions.Period,
                    limit = route.RateLimitOptions.Limit
                }
                : null,
            qosOptions = route.QoSOptions != null
                ? new
                {
                    timeoutValue = route.QoSOptions.TimeoutValue,
                    durationOfBreak = route.QoSOptions.DurationOfBreak
                }
                : null,
            fileCacheOptions = route.FileCacheOptions != null
                ? new { ttlSeconds = route.FileCacheOptions.TtlSeconds }
                : null,
            loadBalancerOptions = route.LoadBalancerOptions != null
                ? new { type = route.LoadBalancerOptions.Type }
                : null
        };
    }
}