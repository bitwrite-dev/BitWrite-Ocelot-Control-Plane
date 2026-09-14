using BitWrite.OcelotControl.Domain.Aggregates.RuntimeInstance;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Infrastructure.Redis;
using StackExchange.Redis;

namespace BitWrite.OcelotControl.Infrastructure.Repositories;

public class RedisRuntimeInstanceRepository : RedisRepositoryBase
{
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
        return new List<RuntimeInstance>();
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
    }

    public async Task UpdateAsync(RuntimeInstance instance, CancellationToken cancellationToken = default)
    {
        await AddAsync(instance, cancellationToken);
    }

    public async Task DeleteAsync(GatewayId gatewayId, CancellationToken cancellationToken = default)
    {
        var key = RedisKeyHelper.RuntimeInstance(gatewayId);
        await DeleteAsync(key);
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