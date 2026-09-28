using BitWrite.OcelotControl.Domain.Aggregates.RuntimeInstance;
using System.Text.Json;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Domain.ValueObjects.Status;
using BitWrite.OcelotControl.Infrastructure.Redis;
using StackExchange.Redis;

namespace BitWrite.OcelotControl.Infrastructure.Repositories;

public class RedisRuntimeInstanceRepository : RedisRepositoryBase, IRuntimeInstanceRepository
{
    private const string RuntimeInstancesIndexKey = "ocelot:index:runtimeinstances";

    public RedisRuntimeInstanceRepository(IConnectionMultiplexer connectionMultiplexer) 
        : base(connectionMultiplexer)
    {
    }

    public async Task<RuntimeInstance?> GetAsync(GatewayId gatewayId, CancellationToken cancellationToken = default)
    {
        var key = RedisKeyHelper.RuntimeInstance(gatewayId);

        // The runtime writes a JSON string here, not a hash. Reading it as a hash
        // found nothing — or threw WRONGTYPE — so no heartbeat was ever observed
        // and anything derived from it was always empty.
        var json = await StringGetAsync(key);

        if (string.IsNullOrWhiteSpace(json))
            return null;

        // The runtime serialises its heartbeat with default (PascalCase) naming,
        // while RedisSerializer reads camelCase. Matching case-insensitively is
        // what lets the document bind; anything stricter reads an empty document
        // and reports the gateway as having no runtime data at all.
        var heartbeat = JsonSerializer.Deserialize<HeartbeatDocument>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (heartbeat is null)
            return null;

        return RuntimeInstance.Reconstitute(
            gatewayId,
            Array.Empty<string>(),
            // The heartbeat does not report a status, so the recorded one stands.
            RuntimeStatus.TryParse(heartbeat.Status, out var status) ? status : RuntimeStatus.Disconnected,
            ParseTimestamp(heartbeat.Timestamp),
            ParseVersion(heartbeat.Version));
    }

    private static DateTimeOffset ParseTimestamp(string? value) =>
        DateTimeOffset.TryParse(value, out var parsed) ? parsed : DateTimeOffset.UnixEpoch;

    private static SnapshotVersion? ParseVersion(string? value) =>
        int.TryParse(value, out var number) ? SnapshotVersion.From(number) : null;

    private sealed record HeartbeatDocument(
        string? GatewayId,
        string? Version,
        string? Status,
        string? Timestamp);

    public async Task<RuntimeInstance?> GetByGatewayIdAsync(GatewayId gatewayId, CancellationToken cancellationToken = default)
    {
        return await GetAsync(gatewayId, cancellationToken);
    }

    public async Task<List<RuntimeInstance>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var gatewayIds = await Database.SortedSetRangeByScoreAsync(
            RuntimeInstancesIndexKey, 
            double.NegativeInfinity, 
            double.PositiveInfinity, 
            Exclude.None, 
            Order.Descending);

        var instances = new List<RuntimeInstance>();

        foreach (var id in gatewayIds)
        {
            try
            {
                var gatewayId = GatewayId.From(Guid.Parse(id.ToString()));
                var instance = await GetAsync(gatewayId, cancellationToken);
                if (instance != null)
                    instances.Add(instance);
            }
            catch
            {
                // Skip invalid IDs
            }
        }

        return instances;
    }

    public async Task AddAsync(RuntimeInstance instance, CancellationToken cancellationToken = default)
    {
        // Written as a JSON string to match what the runtime actually stores.
        // This method is not the source of heartbeats — the runtime writes that
        // key itself — so anything written here would be overwritten by the next
        // beat rather than merged with it.
        var key = RedisKeyHelper.RuntimeInstance(instance.GatewayId);
        var document = new HeartbeatDocument(
            instance.GatewayId.Value.ToString(),
            instance.CurrentVersion?.Value.ToString(),
            instance.Status.Value,
            instance.LastHeartbeat.ToString("O"));

        await StringSetAsync(key, RedisSerializer.Serialize(document));
        await Database.SortedSetAddAsync(
            RuntimeInstancesIndexKey,
            instance.GatewayId.Value.ToString(),
            ToUnixTimestamp(instance.LastHeartbeat));
    }

    public async Task UpdateAsync(RuntimeInstance instance, CancellationToken cancellationToken = default)
    {
        await AddAsync(instance, cancellationToken);
    }

    public async Task DeleteAsync(GatewayId gatewayId, CancellationToken cancellationToken = default)
    {
        var key = RedisKeyHelper.RuntimeInstance(gatewayId);
        await DeleteAsync(key);
        await Database.SortedSetRemoveAsync(RuntimeInstancesIndexKey, gatewayId.Value.ToString());
    }

    private static double ToUnixTimestamp(DateTimeOffset dateTime)
    {
        return dateTime.ToUnixTimeSeconds();
    }
}

public interface IRuntimeInstanceRepository
{
    Task<RuntimeInstance?> GetAsync(GatewayId gatewayId, CancellationToken cancellationToken = default);
    Task<RuntimeInstance?> GetByGatewayIdAsync(GatewayId gatewayId, CancellationToken cancellationToken = default);
    Task<List<RuntimeInstance>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(RuntimeInstance instance, CancellationToken cancellationToken = default);
    Task UpdateAsync(RuntimeInstance instance, CancellationToken cancellationToken = default);
    Task DeleteAsync(GatewayId gatewayId, CancellationToken cancellationToken = default);
}