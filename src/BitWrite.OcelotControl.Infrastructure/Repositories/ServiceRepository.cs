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

        var endpointsJson = GetEntry(entries, "Endpoints");
        var endpoints = !string.IsNullOrEmpty(endpointsJson)
            ? DeserializeEndpoints(endpointsJson)
            : new List<ServiceEndpoint>();

        // Reconstitute, not Create: Create mints a new ServiceId, so reading a
        // service twice returned two different ids and any route referencing it
        // could no longer be resolved.
        return Service.Reconstitute(
            id,
            GetEntry(entries, "Name"),
            GetEntry(entries, "Description"),
            DateTimeOffset.Parse(GetEntry(entries, "CreatedAt")),
            DateTimeOffset.Parse(GetEntry(entries, "UpdatedAt")),
            endpoints);
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
            new("UpdatedAt", service.UpdatedAt.ToString("O")),
            // Endpoints were never persisted, so they could not survive a reload.
            new("Endpoints", RedisSerializer.Serialize(SerializeEndpoints(service.Endpoints)))
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

    /// <summary>
    /// Persisted shape of <see cref="ServiceEndpoint"/>.
    ///
    /// The domain type has `internal` setters, which System.Text.Json will not
    /// write to — deserializing it directly produced objects with empty host and
    /// port 0. The domain deliberately carries no serialization attributes, so the
    /// mapping lives here instead.
    /// </summary>
    private sealed record EndpointRecord(string Host, int Port, int Weight, bool IsActive);

    private static List<ServiceEndpoint> DeserializeEndpoints(string json)
    {
        var records = RedisSerializer.Deserialize<List<EndpointRecord>>(json) ?? new List<EndpointRecord>();

        // Built via Service.Create/AddHost so the domain invariants still apply.
        var carrier = Service.Create("__endpoints__");
        foreach (var record in records)
        {
            if (string.IsNullOrWhiteSpace(record.Host)) continue;
            carrier.AddHost(record.Host, record.Port, weight: record.Weight <= 0 ? 1 : record.Weight);
        }

        return carrier.Endpoints.ToList();
    }

    private static List<EndpointRecord> SerializeEndpoints(IReadOnlyList<ServiceEndpoint> endpoints) =>
        endpoints
            .Select(e => new EndpointRecord(e.Host, e.Port, e.Weight, e.IsActive))
            .ToList();
}
