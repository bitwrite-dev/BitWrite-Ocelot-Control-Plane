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
        var json = await StringGetAsync(RedisKeyHelper.RuntimeInstance(gatewayId));

        if (string.IsNullOrWhiteSpace(json))
            return null;

        // The instance record is a JSON string, not a hash. Reading it as a hash
        // found nothing — or threw WRONGTYPE — so no gateway was ever observed.
        var instance = Deserialize<InstanceDocument>(json);

        if (instance is null)
            return null;

        // What the runtime last reported lives on its own key, because it is the
        // runtime's to write and it carries no status: when it shared the instance
        // key, each beat replaced a recorded status with nothing and a gateway that
        // had just applied a configuration read back as disconnected. Records
        // written before the split have no heartbeat key, so the instance's own
        // timestamp stands in for them.
        var heartbeatJson = await StringGetAsync(RedisKeyHelper.RuntimeGatewayHeartbeat(gatewayId));
        var heartbeat = string.IsNullOrWhiteSpace(heartbeatJson)
            ? null
            : Deserialize<HeartbeatDocument>(heartbeatJson);

        // The runtime serialises with default (PascalCase) naming, while
        // RedisSerializer reads camelCase. Matching case-insensitively is what lets
        // the documents bind; anything stricter reads nothing at all.
        return RuntimeInstance.Reconstitute(
            gatewayId,
            Array.Empty<string>(),
            RuntimeStatus.TryParse(instance.Status, out var status)
                ? status
                : RuntimeStatus.Disconnected,
            ParseTimestamp(heartbeat?.Timestamp ?? instance.Timestamp),
            ParseVersion(heartbeat?.Version ?? instance.Version));
    }

    private static T? Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

    private static DateTimeOffset ParseTimestamp(string? value) =>
        DateTimeOffset.TryParse(value, out var parsed) ? parsed : DateTimeOffset.UnixEpoch;

    private static SnapshotVersion? ParseVersion(string? value) =>
        int.TryParse(value, out var number) ? SnapshotVersion.From(number) : null;

    /// <summary>What the control plane recorded. Carries the status.</summary>
    private sealed record InstanceDocument(
        string? GatewayId,
        string? Version,
        string? Status,
        string? Timestamp);

    /// <summary>What the runtime last reported. Carries no status — it cannot know one.</summary>
    private sealed record HeartbeatDocument(
        string? GatewayId,
        string? Version,
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
        // The instance record, on the key the control plane owns. The runtime writes
        // its heartbeat to a different key, so nothing here is overwritten by the
        // next beat.
        var key = RedisKeyHelper.RuntimeInstance(instance.GatewayId);
        var document = new InstanceDocument(
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