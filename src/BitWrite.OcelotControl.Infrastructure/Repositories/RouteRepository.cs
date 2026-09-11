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
            ? RedisSerializer.Deserialize<List<DownstreamTarget>>(targetsJson) 
            : new List<DownstreamTarget>();

        var route = Route.Create(
            routeKey.Method,
            routeKey.Path,
            serviceId,
            targets,
            routeKey.Host != "" ? routeKey.Host : null,
            routeKey.Host,
            string.Empty // correlationId
        );

        // Override the auto-generated ID
        var routeIdField = typeof(Route).GetProperty("Id");
        routeIdField?.SetValue(route, id);

        // Add downstream targets if present
        foreach (var target in targets)
        {
            route.AddDownstreamTarget(target);
        }

        return route;
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
            new("Key", route.RouteKey.ToSignature()),
            new("Method", route.Method.Value),
            new("UpstreamPath", route.UpstreamPath.Value),
            new("Host", route.Host ?? ""),
            new("ServiceId", route.ServiceId.Value.ToString()),
            new("IsEnabled", route.IsEnabled.ToString()),
            new("DownstreamTargets", RedisSerializer.Serialize(route.DownstreamTargets)),
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
}