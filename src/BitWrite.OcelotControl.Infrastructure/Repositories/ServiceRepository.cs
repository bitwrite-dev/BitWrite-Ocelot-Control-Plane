using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Domain.Aggregates.Service;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Infrastructure.Redis;
using StackExchange.Redis;

namespace BitWrite.OcelotControl.Infrastructure.Repositories;

public class RedisServiceRepository : RedisRepositoryBase, IServiceRepository
{
    public RedisServiceRepository(IConnectionMultiplexer connectionMultiplexer) 
        : base(connectionMultiplexer)
    {
    }

    public async Task<Service?> GetAsync(ServiceId id, CancellationToken cancellationToken = default)
    {
        var key = RedisKeyHelper.Service(id);
        var entries = await GetHashAsync(key);
        
        if (entries.Length == 0)
            return null;

        var service = Service.Create(
            id,
            GetEntry(entries, "Name"),
            string.Empty // correlationId
        );

        return service;
    }

    public async Task<List<Service>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var serviceIds = await SetMembersAsync("ocelot:index:services");
        var services = new List<Service>();

        foreach (var id in serviceIds)
        {
            try
            {
                var serviceId = ServiceId.From(id.ToString());
                var service = await GetAsync(serviceId, cancellationToken);
                if (service != null)
                    services.Add(service);
            }
            catch
            {
                // Skip invalid IDs
            }
        }

        return services;
    }

    public async Task AddAsync(Service service, CancellationToken cancellationToken = default)
    {
        var key = RedisKeyHelper.Service(service.Id);
        var entries = new HashEntry[]
        {
            new("Id", service.Id.Value.ToString()),
            new("Name", service.Name),
            new("Description", service.Description ?? ""),
            new("CreatedAt", service.CreatedAt.ToString("O")),
            new("UpdatedAt", service.UpdatedAt.ToString("O"))
        };

        await SetHashAsync(key, entries);
        await SetAddAsync("ocelot:index:services", service.Id.Value.ToString());
    }

    public async Task UpdateAsync(Service service, CancellationToken cancellationToken = default)
    {
        await AddAsync(service, cancellationToken);
    }

    public async Task DeleteAsync(ServiceId id, CancellationToken cancellationToken = default)
    {
        var key = RedisKeyHelper.Service(id);
        await DeleteAsync(key);
        await SetRemoveAsync("ocelot:index:services", id.Value.ToString());
    }
}