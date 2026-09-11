using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Domain.Aggregates.Gateway;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Infrastructure.Redis;
using StackExchange.Redis;

namespace BitWrite.OcelotControl.Infrastructure.Repositories;

public class RedisGatewayRepository : RedisRepositoryBase, IGatewayRepository
{
    public RedisGatewayRepository(IConnectionMultiplexer connectionMultiplexer) 
        : base(connectionMultiplexer)
    {
    }

    public async Task<Gateway?> GetAsync(GatewayId id, CancellationToken cancellationToken = default)
    {
        var key = RedisKeyHelper.Gateway(id);
        var entries = await GetHashAsync(key);
        
        if (entries.Length == 0)
            return null;

        var gateway = Gateway.Register(
            id,
            GetEntry(entries, "Name"),
            GetEntry(entries, "Description")
        );

        // Update status if present
        var statusEntry = GetEntry(entries, "Status");
        if (!string.IsNullOrEmpty(statusEntry))
        {
            // Gateway status would need to be set via the aggregate's methods
        }

        return gateway;
    }

    public async Task<List<Gateway>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var gatewayIds = await SetMembersAsync("ocelot:index:gateways");
        var gateways = new List<Gateway>();

        foreach (var id in gatewayIds)
        {
            try
            {
                var gatewayId = GatewayId.From(id.ToString());
                var gateway = await GetAsync(gatewayId, cancellationToken);
                if (gateway != null)
                    gateways.Add(gateway);
            }
            catch
            {
                // Skip invalid IDs
            }
        }

        return gateways;
    }

    public async Task AddAsync(Gateway gateway, CancellationToken cancellationToken = default)
    {
        var key = RedisKeyHelper.Gateway(gateway.Id);
        var entries = new HashEntry[]
        {
            new("Id", gateway.Id.Value.ToString()),
            new("Name", gateway.Name),
            new("Description", gateway.Description ?? ""),
            new("Status", gateway.Status.Value),
            new("CreatedAt", gateway.CreatedAt.ToString("O")),
            new("UpdatedAt", gateway.UpdatedAt.ToString("O"))
        };

        await SetHashAsync(key, entries);
        await SetAddAsync("ocelot:index:gateways", gateway.Id.Value.ToString());
    }

    public async Task UpdateAsync(Gateway gateway, CancellationToken cancellationToken = default)
    {
        await AddAsync(gateway, cancellationToken);
    }

    public async Task DeleteAsync(GatewayId id, CancellationToken cancellationToken = default)
    {
        var key = RedisKeyHelper.Gateway(id);
        await DeleteAsync(key);
        await SetRemoveAsync("ocelot:index:gateways", id.Value.ToString());
    }
}