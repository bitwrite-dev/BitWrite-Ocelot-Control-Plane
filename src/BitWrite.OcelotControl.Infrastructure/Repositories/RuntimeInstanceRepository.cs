using BitWrite.OcelotControl.Domain.Aggregates.RuntimeInstance;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Infrastructure.Redis;
using StackExchange.Redis;

namespace BitWrite.OcelotControl.Infrastructure.Repositories;

public class RedisRuntimeInstanceRepository : RedisRepositoryBase
{
    private const string RuntimeInstancesIndexKey = "ocelot:index:runtimeinstances";

    public RedisRuntimeInstanceRepository(IConnectionMultiplexer connectionMultiplexer) 
        : base(connectionMultiplexer)
    {
    }

    public async Task<RuntimeInstance?> GetAsync(GatewayId gatewayId, CancellationToken cancellationToken = default)
    {
        var key = RedisKeyHelper.RuntimeInstance(gatewayId);
        var entries = await GetHashAsync(key);
        
        if (entries.Length == 0)
            return null;

        var capabilitiesStr = GetEntry(entries, "Capabilities");
        var capabilities = !string.IsNullOrEmpty(capabilitiesStr) 
            ? capabilitiesStr.Split(',', StringSplitOptions.RemoveEmptyEntries) 
            : Array.Empty<string>();

        var instance = RuntimeInstance.Register(
            GatewayId.From(GetEntry(entries, "GatewayId")),
            capabilities,
            string.Empty
        );

        return instance;
    }

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
        var key = RedisKeyHelper.RuntimeInstance(instance.GatewayId);
        var entries = new HashEntry[]
        {
            new("GatewayId", instance.GatewayId.Value.ToString()),
            new("Capabilities", string.Join(",", instance.Capabilities)),
            new("Status", instance.Status.Value),
            new("LastHeartbeatAt", instance.LastHeartbeat.ToString("O")),
        };

        await SetHashAsync(key, entries);
        await Database.SortedSetAddAsync(RuntimeInstancesIndexKey, instance.GatewayId.Value.ToString(), ToUnixTimestamp(instance.LastHeartbeat));
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