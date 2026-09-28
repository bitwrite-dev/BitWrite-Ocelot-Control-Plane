using BitWrite.OcelotControl.Domain.Exceptions;

namespace BitWrite.OcelotControl.Domain.ValueObjects.Configuration;

/// <summary>
/// How the gateway's HTTP client behaves when calling a downstream service.
/// </summary>
/// <remarks>
/// Named after Ocelot's own route property, <c>HttpHandlerOptions</c>, so the
/// generated configuration is recognisable when read next to Ocelot's
/// documentation. Ocelot's name says "handler", which is misleading — these are
/// settings for the client that makes the request, not for a handler.
/// </remarks>
public record HttpClientOptions : ValueObject
{
    /// <summary>Whether to follow redirects.</summary>
    public bool AllowAutoRedirect { get; init; }

    /// <summary>Connections kept open per downstream server.</summary>
    public int MaxConnectionsPerServer { get; init; } = int.MaxValue;

    /// <summary>Seconds before a pooled connection is recycled.</summary>
    public int PooledConnectionLifetimeSeconds { get; init; } = 120;

    /// <summary>Share a cookie container across routes to the same service.</summary>
    public bool UseCookieContainer { get; init; }

    public bool UseProxy { get; init; }

    public bool UseTracing { get; init; }

    private HttpClientOptions() { }

    public static HttpClientOptions Create(
        bool allowAutoRedirect = false,
        int? maxConnectionsPerServer = null,
        int? pooledConnectionLifetimeSeconds = null,
        bool useCookieContainer = false,
        bool useProxy = false,
        bool useTracing = false)
    {
        if (maxConnectionsPerServer is <= 0)
        {
            throw new DomainException(
                "Max connections per server must be positive",
                "INVALID_MAX_CONNECTIONS");
        }

        if (pooledConnectionLifetimeSeconds is <= 0)
        {
            throw new DomainException(
                "Pooled connection lifetime must be positive",
                "INVALID_CONNECTION_LIFETIME");
        }

        return new HttpClientOptions
        {
            AllowAutoRedirect = allowAutoRedirect,
            MaxConnectionsPerServer = maxConnectionsPerServer ?? int.MaxValue,
            PooledConnectionLifetimeSeconds = pooledConnectionLifetimeSeconds ?? 120,
            UseCookieContainer = useCookieContainer,
            UseProxy = useProxy,
            UseTracing = useTracing
        };
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return AllowAutoRedirect;
        yield return MaxConnectionsPerServer;
        yield return PooledConnectionLifetimeSeconds;
        yield return UseCookieContainer;
        yield return UseProxy;
        yield return UseTracing;
    }
}
