using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Domain.Aggregates.Gateway;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Domain.ValueObjects.Status;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Infrastructure.Redis;
using StackExchange.Redis;

namespace BitWrite.OcelotControl.Infrastructure.Repositories;

public class RedisGatewayRepository : RedisRepositoryBase, IGatewayRepository
{
    public RedisGatewayRepository(IConnectionMultiplexer connectionMultiplexer, IEnvironmentContext environmentContext) 
        : base(connectionMultiplexer, environmentContext)
    {
    }

    /// <summary>
    /// A missing or unparseable timestamp falls back to the epoch rather than
    /// failing the read, since a gateway is still usable without one.
    /// </summary>
    private static DateTimeOffset ParseTimestamp(string value) =>
        DateTimeOffset.TryParse(value, out var parsed)
            ? parsed
            : DateTimeOffset.UnixEpoch;

public async Task<Gateway?> GetAsync(GatewayId id, CancellationToken cancellationToken = default)
    {
        var key = RedisKeyHelper.Gateway(id);
        var entries = await GetHashAsync(key);

        if (entries.Length == 0)
            return null;

        // Reconstitute, not Register: Register mints a new id, which made this
        // read return a gateway under a different key than the one it was loaded
        // from. A subsequent write then created a second row and the next read
        // of the original key reported the gateway as missing.
        var statusEntry = GetEntry(entries, "Status");

        // A row written before the environment existed has no field for it, so it
        // reads as development. That is the honest answer: the gateway was created
        // before environments were a thing, and development is what it served.
        var environmentEntry = GetEntry(entries, "Environment");
        var environment = !string.IsNullOrEmpty(environmentEntry)
            ? EnvironmentName.From(environmentEntry)
            : EnvironmentName.From("development");

        return Gateway.Reconstitute(
            id,
            GetEntry(entries, "Name"),
            GetEntry(entries, "Description") is { Length: > 0 } storedDescription
                ? storedDescription
                : null,
            // A row written before the status existed, or one holding a value
            // this build no longer knows, reads as Disconnected rather than
            // failing the whole gateway.
            RuntimeStatus.TryParse(statusEntry, out var status) ? status : RuntimeStatus.Disconnected,
            environment,
            ParseTimestamp(GetEntry(entries, "CreatedAt")),
            ParseTimestamp(GetEntry(entries, "UpdatedAt")));
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
            new("Environment", gateway.Environment.Value),
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