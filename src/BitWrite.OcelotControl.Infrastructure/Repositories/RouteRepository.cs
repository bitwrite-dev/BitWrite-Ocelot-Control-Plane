using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Domain.Aggregates.Route;
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
    public RedisRouteRepository(IConnectionMultiplexer connectionMultiplexer) 
        : base(connectionMultiplexer)
    {
    }

    public async Task<Route?> GetAsync(RouteId id, CancellationToken cancellationToken = default)
    {
        var key = RedisKeyHelper.Route(id);
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
            host: GetEntry(entries, "Host"));
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
        var key = RedisKeyHelper.Route(route.Id);
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
            new("DownstreamTargets", RedisSerializer.Serialize(SerializeTargets(route.DownstreamTargets))),
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
            var key = RedisKeyHelper.Route(id);
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
}
