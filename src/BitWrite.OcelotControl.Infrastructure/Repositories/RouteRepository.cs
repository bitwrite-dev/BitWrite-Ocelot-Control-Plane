using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Domain.Aggregates.Route;
using BitWrite.OcelotControl.Domain.Exceptions;
using BitWrite.OcelotControl.Domain.ValueObjects.FeatureConfig;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Infrastructure.Redis;
using StackExchange.Redis;
using HttpMethod = BitWrite.OcelotControl.Domain.ValueObjects.Configuration.HttpMethod;
using RouteId = BitWrite.OcelotControl.Domain.ValueObjects.Identity.RouteId;
using ServiceId = BitWrite.OcelotControl.Domain.ValueObjects.Identity.ServiceId;

namespace BitWrite.OcelotControl.Infrastructure.Repositories;

public class RedisRouteRepository : RedisRepositoryBase, IRouteRepository
{
    public RedisRouteRepository(IConnectionMultiplexer connectionMultiplexer, IEnvironmentContext environmentContext) 
        : base(connectionMultiplexer, environmentContext)
    {
    }

    public async Task<Route?> GetAsync(RouteId id, CancellationToken cancellationToken = default)
    {
        var key = RedisKeyHelper.Route(id, Environment);
        var entries = await GetHashAsync(key);
        
        if (entries.Length == 0)
            return null;

        var routeKeyStr = GetEntry(entries, "Key");
        var routeKey = !string.IsNullOrEmpty(routeKeyStr) 
            ? RouteKey.Parse(routeKeyStr)
            : RouteKey.Create(
                HttpMethod.Parse(GetEntry(entries, "Method")),
                UpstreamPath.From(GetEntry(entries, "UpstreamPath")),
                GetEntry(entries, "Host"));

        var serviceId = ServiceId.From(GetEntry(entries, "ServiceId"));
        var targetsJson = GetEntry(entries, "DownstreamTargets");
        var targets = !string.IsNullOrEmpty(targetsJson)
            ? DeserializeTargets(targetsJson)
            : new List<DownstreamTarget>();

        // Reconstitute, not Create. Create mints a fresh RouteId, forces
        // IsEnabled back to true and re-stamps the timestamps; the previous code
        // worked around the id with reflection, passed the host into the `key`
        // parameter slot, and re-added targets that Create had already added.
        return Route.Reconstitute(
            id,
            routeKey.Method,
            routeKey.Path,
            serviceId,
            targets,
            bool.TryParse(GetEntry(entries, "IsEnabled"), out var isEnabled) && isEnabled,
            DateTimeOffset.Parse(GetEntry(entries, "CreatedAt")),
            DateTimeOffset.Parse(GetEntry(entries, "UpdatedAt")),
            // Fall back to the signature for rows written before FriendlyKey existed.
            key: GetEntry(entries, "FriendlyKey") is { Length: > 0 } friendly
                ? friendly
                : GetEntry(entries, "Key"),
            host: GetEntry(entries, "Host"),
            priority: int.TryParse(GetEntry(entries, "Priority"), out var storedPriority)
                ? storedPriority
                : 0,
            routeIsCaseSensitive:
                bool.TryParse(GetEntry(entries, "RouteIsCaseSensitive"), out var storedCase)
                    && storedCase,
            downstreamTemplate: ParseStoredTemplate(GetEntry(entries, "DownstreamTemplate")),
            downstreamMethod: ParseStoredMethod(GetEntry(entries, "DownstreamMethod")),
            downstreamHttpVersion: NullIfEmpty(GetEntry(entries, "DownstreamHttpVersion")),
            downstreamHttpVersionPolicy: NullIfEmpty(GetEntry(entries, "DownstreamHttpVersionPolicy")),
            acceptAnyServerCertificate:
                bool.TryParse(GetEntry(entries, "DangerousAcceptAnyServerCertificateValidator"), out var storedAcceptAny)
                    && storedAcceptAny,
            delegatingHandlers: DeserializeList(GetEntry(entries, "DelegatingHandlers")),
            httpClientOptions: DeserializeHttpClientOptions(GetEntry(entries, "HttpClientOptions")),
            timeoutSeconds: ParseStoredTimeout(GetEntry(entries, "TimeoutSeconds")),
            authenticationOptions: DeserializeAuthentication(GetEntry(entries, "AuthenticationOptions")),
            authorizationOptions: DeserializeAuthorization(GetEntry(entries, "AuthorizationOptions")),
            rateLimitOptions: DeserializeRateLimit(GetEntry(entries, "RateLimitOptions")),
            qosOptions: DeserializeQoS(GetEntry(entries, "QoSOptions")),
            cacheOptions: DeserializeCache(GetEntry(entries, "CacheOptions")),
            loadBalancerOptions: DeserializeLoadBalancer(GetEntry(entries, "LoadBalancerOptions")),
            headerOptions: DeserializeTransforms<HeaderOptions, HeaderTransform>(
                GetEntry(entries, "HeaderOptions"),
                HeaderTransform.Create,
                HeaderOptions.Create),
            claimOptions: DeserializeTransforms<ClaimOptions, ClaimTransform>(
                GetEntry(entries, "ClaimOptions"),
                ClaimTransform.Create,
                ClaimOptions.Create),
            queryOptions: DeserializeTransforms<QueryOptions, QueryTransform>(
                GetEntry(entries, "QueryOptions"),
                QueryTransform.Create,
                QueryOptions.Create));
    }

    public async Task<List<Route>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var routeIds = await SetMembersAsync(RedisKeyHelper.IndexRoutes);
        var routes = new List<Route>();

        foreach (var id in routeIds)
        {
            try
            {
                var routeId = RouteId.From(id.ToString());
                var route = await GetAsync(routeId, cancellationToken);
                if (route != null)
                    routes.Add(route);
            }
            catch
            {
                // Skip invalid IDs
            }
        }

        return routes;
    }

    public async Task AddAsync(Route route, CancellationToken cancellationToken = default)
    {
        var key = RedisKeyHelper.Route(route.Id, Environment);
        var entries = new HashEntry[]
        {
            new("Id", route.Id.Value.ToString()),
            // "Key" holds the composite RouteKey signature. The caller-supplied
            // friendly key was never persisted, so it was replaced by that
            // signature on every read; it lives in its own field now.
            new("Key", route.RouteKey.ToSignature()),
            new("FriendlyKey", route.Key ?? ""),
            new("Method", route.Method.Value),
            new("UpstreamPath", route.UpstreamPath.Value),
            new("Host", route.Host ?? ""),
            new("ServiceId", route.ServiceId.Value.ToString()),
            new("IsEnabled", route.IsEnabled.ToString()),
            new("Priority", route.Priority.ToString()),
            new("RouteIsCaseSensitive", route.RouteIsCaseSensitive.ToString()),
            new("DownstreamTemplate", route.DownstreamTemplate?.Value ?? ""),
            new("DownstreamMethod", route.DownstreamMethod?.Value ?? ""),
            new("DownstreamHttpVersion", route.DownstreamHttpVersion ?? ""),
            new("DownstreamHttpVersionPolicy", route.DownstreamHttpVersionPolicy ?? ""),
            new("DangerousAcceptAnyServerCertificateValidator", route.DangerousAcceptAnyServerCertificateValidator.ToString()),
            new("DelegatingHandlers", RedisSerializer.Serialize(route.DelegatingHandlers.ToList())),
            new("HttpClientOptions", Serialize(route.HttpClientOptions, o => new HttpClientRecord(
                o.AllowAutoRedirect,
                o.MaxConnectionsPerServer,
                o.PooledConnectionLifetimeSeconds,
                o.UseCookieContainer,
                o.UseProxy,
                o.UseTracing))),
            new("TimeoutSeconds", route.TimeoutSeconds?.ToString() ?? ""),
            new("DownstreamTargets", RedisSerializer.Serialize(SerializeTargets(route.DownstreamTargets))),
            // Every feature config is persisted. They were all missing, so a
            // route came back from storage with nothing configured and the
            // response that followed a create — built from the in-memory
            // aggregate — disagreed with the next read. See #476.
            new("AuthenticationOptions", Serialize(route.AuthenticationOptions, o => new AuthenticationRecord(o.Scheme, o.Provider, o.Properties))),
            new("AuthorizationOptions", Serialize(route.AuthorizationOptions, o => new AuthorizationRecord(o.Policies, o.Scopes, o.Requirements))),
            new("RateLimitOptions", Serialize(route.RateLimitOptions, o => new RateLimitRecord(o.Limit, o.Period, o.PeriodSeconds, o.ClientIdHeader, o.Whitelist))),
            new("QoSOptions", Serialize(route.QoSOptions, o => new QoSRecord(o.TimeoutSeconds, o.RetryCount, o.UseCircuitBreaker, o.CircuitBreakerTimeoutSeconds, o.CircuitBreakerExceptionsAllowedBeforeBreaking))),
            new("CacheOptions", Serialize(route.CacheOptions, o => new CacheRecord(o.TtlSeconds, o.Key, o.Region, o.HeaderNames))),
            new("LoadBalancerOptions", Serialize(route.LoadBalancerOptions, o => new LoadBalancerRecord(o.Algorithm, o.Key))),
            new("HeaderOptions", Serialize(route.HeaderOptions, o => new TransformRecord(ToPairs(o.Add), o.Remove, ToPairs(o.Transform)))),
            new("ClaimOptions", Serialize(route.ClaimOptions, o => new TransformRecord(ToPairs(o.Add), o.Remove, ToPairs(o.Transform)))),
            new("QueryOptions", Serialize(route.QueryOptions, o => new TransformRecord(ToPairs(o.Add), o.Remove, ToPairs(o.Transform)))),
            new("CreatedAt", route.CreatedAt.ToString("O")),
            new("UpdatedAt", route.UpdatedAt.ToString("O"))
        };

        await SetHashAsync(key, entries);
        await SetAddAsync(RedisKeyHelper.IndexRoutes, route.Id.Value.ToString());
        
        // Update service-route index
        await SetAddAsync(RedisKeyHelper.IndexServiceRoutes(route.ServiceId), route.Id.Value.ToString());
        
        // Update route signature index
        var signature = route.RouteKey.ToSignature();
        await StringSetAsync(RedisKeyHelper.IndexRouteSignature(signature), route.Id.Value.ToString());
    }

    public async Task UpdateAsync(Route route, CancellationToken cancellationToken = default)
    {
        await AddAsync(route, cancellationToken);
    }

    public async Task DeleteAsync(RouteId id, CancellationToken cancellationToken = default)
    {
        var route = await GetAsync(id, cancellationToken);
        if (route != null)
        {
            var key = RedisKeyHelper.Route(id, Environment);
            await DeleteAsync(key);
            await SetRemoveAsync(RedisKeyHelper.IndexRoutes, id.Value.ToString());
            await SetRemoveAsync(RedisKeyHelper.IndexServiceRoutes(route.ServiceId), id.Value.ToString());
            var signature = route.RouteKey.ToSignature();
            await StringSetAsync(RedisKeyHelper.IndexRouteSignature(signature), "");
        }
    }

    /// <summary>
    /// Persisted shape of <see cref="DownstreamTarget"/>.
    ///
    /// The domain record has a private constructor and no [JsonConstructor], so
    /// System.Text.Json refuses to deserialize it and the read threw — which
    /// GetAllAsync swallowed, making the whole route list come back empty. The
    /// domain carries no serialization attributes by design, so the mapping lives
    /// here.
    /// </summary>
    private sealed record TargetRecord(string Scheme, string Host, int Port, string Path);

    private static List<DownstreamTarget> DeserializeTargets(string json)
    {
        var records = RedisSerializer.Deserialize<List<TargetRecord>>(json) ?? new List<TargetRecord>();

        return records
            .Where(r => !string.IsNullOrWhiteSpace(r.Host))
            .Select(r => DownstreamTarget.Create(
                string.IsNullOrWhiteSpace(r.Scheme) ? "http" : r.Scheme,
                r.Host,
                r.Port,
                string.IsNullOrWhiteSpace(r.Path) ? "/" : r.Path))
            .ToList();
    }

    private static List<TargetRecord> SerializeTargets(IReadOnlyList<DownstreamTarget> targets) =>
        targets
            .Select(t => new TargetRecord(t.Scheme, t.Host, t.Port, t.Path))
            .ToList();

    // --- Feature config persistence -----------------------------------------
    //
    // Each feature config is a domain record with a private constructor and no
    // serialization attributes, so System.Text.Json cannot rebuild one. They are
    // persisted as plain records and mapped back through the domain factory,
    // which is also what re-applies the value's own validation.

    private sealed record HttpClientRecord(
        bool AllowAutoRedirect,
        int MaxConnectionsPerServer,
        int PooledConnectionLifetimeSeconds,
        bool UseCookieContainer,
        bool UseProxy,
        bool UseTracing);

    private sealed record AuthenticationRecord(string? Scheme, string? Provider, Dictionary<string, string>? Properties);

    private sealed record AuthorizationRecord(List<string>? Policies, List<string>? Scopes, Dictionary<string, string>? Requirements);

    private sealed record RateLimitRecord(int? Limit, string? Period, int? PeriodSeconds, string? ClientIdHeader, List<string>? Whitelist);

    private sealed record QoSRecord(int? TimeoutSeconds, int? RetryCount, bool? UseCircuitBreaker, int? CircuitBreakerTimeoutSeconds, int? CircuitBreakerExceptionsAllowedBeforeBreaking);

    private sealed record CacheRecord(int TtlSeconds, string? Key, string? Region, List<string>? HeaderNames);

    private sealed record LoadBalancerRecord(string? Algorithm, string? Key);

    private sealed record PairRecord(string Key, string Value);

    /// <summary>
    /// Projects any of the three transform flavours into the shared pair shape.
    /// They are distinct types with the same key and value, which is why one
    /// record serves all of them.
    /// </summary>
    private static List<PairRecord>? ToPairs<T>(List<T>? transforms)
        where T : class
    {
        if (transforms == null) return null;

        return transforms
            .Select(t => new PairRecord(GetValue(t, "Key"), GetValue(t, "Value")))
            .ToList();

        static string GetValue(object target, string property) =>
            target.GetType().GetProperty(property)?.GetValue(target) as string ?? string.Empty;
    }

    private sealed record TransformRecord(List<PairRecord>? Add, List<string>? Remove, List<PairRecord>? Transform);

    /// <summary>
    /// Serialises a feature config, or stores an empty string for "not
    /// configured" so the field is absent rather than a null entry.
    /// </summary>
    private static string Serialize<TValue, TRecord>(TValue? value, Func<TValue, TRecord> project)
        where TValue : class
    {
        if (value == null) return string.Empty;
        return RedisSerializer.Serialize(project(value));
    }

    /// <summary>
    /// Deserialises a stored record, or null when the field is absent.
    /// </summary>
    /// <remarks>
    /// An unconfigured option is written as an empty string, and handing that to
    /// System.Text.Json throws rather than returning null — which would fail the
    /// whole read and empty the route list.
    /// </remarks>
    private static TRecord? TryDeserialize<TRecord>(string json)
        where TRecord : class
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        return RedisSerializer.Deserialize<TRecord>(json);
    }

    /// <summary>An empty stored entry means "not set", not an empty value.</summary>
    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrEmpty(value) ? null : value;

    /// <summary>
    /// Restores the downstream verb, ignoring a stored value that no longer parses.
    /// </summary>
    /// <remarks>
    /// Silently dropping it keeps a route loadable if a verb is ever removed from
    /// the allowed set. Storing a value the domain would reject would make the row
    /// unreadable, which is worse than losing one optional setting.
    /// </remarks>
    private static HttpMethod? ParseStoredMethod(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        try
        {
            return HttpMethod.Parse(value);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Restores the downstream template, ignoring a stored value that no longer
    /// parses so the row stays readable.
    /// </summary>
    private static DownstreamPathTemplate? ParseStoredTemplate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        try
        {
            return DownstreamPathTemplate.From(value);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static List<string>? DeserializeList(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        return RedisSerializer.Deserialize<List<string>>(json);
    }

    private static int? ParseStoredTimeout(string? value) =>
        int.TryParse(value, out var seconds) && seconds > 0 ? seconds : null;

    private static HttpClientOptions? DeserializeHttpClientOptions(string json)
    {
        var record = TryDeserialize<HttpClientRecord>(json);
        if (record is null) return null;

        return HttpClientOptions.Create(
            record.AllowAutoRedirect,
            record.MaxConnectionsPerServer,
            record.PooledConnectionLifetimeSeconds,
            record.UseCookieContainer,
            record.UseProxy,
            record.UseTracing);
    }

    private static AuthenticationOptions? DeserializeAuthentication(string json)
    {
        var record = TryDeserialize<AuthenticationRecord>(json);
        // A record written before this field existed has no scheme, and the
        // domain factory rejects an empty one, so there is nothing to restore.
        if (record == null || string.IsNullOrWhiteSpace(record.Scheme)) return null;

        try
        {
            return AuthenticationOptions.Create(
                record.Scheme,
                record.Provider,
                record.Properties);
        }
        catch (DomainException)
        {
            return null;
        }
    }

    private static AuthorizationOptions? DeserializeAuthorization(string json)
    {
        var record = TryDeserialize<AuthorizationRecord>(json);
        if (record == null) return null;

        try
        {
            return AuthorizationOptions.Create(record.Policies, record.Scopes, record.Requirements);
        }
        catch (DomainException)
        {
            return null;
        }
    }

    private static RateLimitOptions? DeserializeRateLimit(string json)
    {
        var record = TryDeserialize<RateLimitRecord>(json);
        // Both of these are required by the factory.
        if (record == null || record.Limit is null || string.IsNullOrWhiteSpace(record.Period)) return null;

        try
        {
            return RateLimitOptions.Create(record.Limit.Value, record.Period, record.ClientIdHeader, record.Whitelist);
        }
        catch (DomainException)
        {
            return null;
        }
    }

    private static QoSOptions? DeserializeQoS(string json)
    {
        var record = TryDeserialize<QoSRecord>(json);
        if (record == null) return null;

        try
        {
            return QoSOptions.Create(
                record.TimeoutSeconds,
                record.RetryCount,
                record.UseCircuitBreaker,
                record.CircuitBreakerTimeoutSeconds,
                record.CircuitBreakerExceptionsAllowedBeforeBreaking);
        }
        catch (DomainException)
        {
            return null;
        }
    }

    private static CacheOptions? DeserializeCache(string json)
    {
        var record = TryDeserialize<CacheRecord>(json);
        if (record == null || record.TtlSeconds <= 0) return null;

        try
        {
            return CacheOptions.Create(record.TtlSeconds, record.Key, record.Region, record.HeaderNames);
        }
        catch (DomainException)
        {
            return null;
        }
    }

    private static LoadBalancerOptions? DeserializeLoadBalancer(string json)
    {
        var record = TryDeserialize<LoadBalancerRecord>(json);
        if (record == null || string.IsNullOrWhiteSpace(record.Algorithm)) return null;

        try
        {
            return LoadBalancerOptions.Create(record.Algorithm, record.Key);
        }
        catch (DomainException)
        {
            return null;
        }
    }

    /// <summary>
    /// Rebuilds a transformation block, which shares one shape across headers,
    /// claims and query strings.
    /// </summary>
    /// <param name="makeTransform">
    /// The domain factory for that flavour of transform; all three take a key
    /// and a value, but they are distinct types.
    /// </param>
    private static TOptions? DeserializeTransforms<TOptions, TTransform>(
        string json,
        Func<string, string, TTransform> makeTransform,
        Func<List<TTransform>?, List<string>?, List<TTransform>?, TOptions> build)
        where TOptions : class
    {
        var record = TryDeserialize<TransformRecord>(json);
        if (record == null) return null;

        try
        {
            List<TTransform>? Add() =>
                record.Add?.Select(pair => makeTransform(pair.Key, pair.Value)).ToList();

            List<TTransform>? Transform() =>
                record.Transform?.Select(pair => makeTransform(pair.Key, pair.Value)).ToList();

            return build(Add(), record.Remove, Transform());
        }
        catch (DomainException)
        {
            // A stored value the domain no longer accepts is dropped rather than
            // failing the whole read, which would empty the route list.
            return null;
        }
    }
}
