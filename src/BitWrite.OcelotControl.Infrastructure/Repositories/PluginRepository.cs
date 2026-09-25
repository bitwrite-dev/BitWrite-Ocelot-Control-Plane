using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Domain.Aggregates.Plugin;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Domain.ValueObjects.Status;
using BitWrite.OcelotControl.Infrastructure.Redis;
using StackExchange.Redis;

namespace BitWrite.OcelotControl.Infrastructure.Repositories;

public class RedisPluginRepository : RedisRepositoryBase, IPluginRepository
{
    private const string PluginsIndexKey = "ocelot:index:plugins";

    public RedisPluginRepository(IConnectionMultiplexer connectionMultiplexer) 
        : base(connectionMultiplexer)
    {
    }

    public async Task<Plugin?> GetAsync(PluginId id, CancellationToken cancellationToken = default)
    {
        var key = RedisKeyHelper.Plugin(id);
        var entries = await GetHashAsync(key);
        
        if (entries.Length == 0)
            return null;

        var scopeStr = GetEntry(entries, "Scope");
        var scope = !string.IsNullOrEmpty(scopeStr) 
            ? PluginScope.From(scopeStr) 
            : PluginScope.Global;

        var plugin = Plugin.Install(
            id.Value.ToString(),
            GetEntry(entries, "Name"),
            GetEntry(entries, "Version"),
            GetEntry(entries, "Description"),
            scope
        );

        var isEnabled = bool.TryParse(GetEntry(entries, "IsEnabled"), out var enabled) && enabled;
        if (!isEnabled)
            plugin.Disable();

        return plugin;
    }

    public async Task<List<Plugin>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var pluginIds = await Database.SortedSetRangeByScoreAsync(
            PluginsIndexKey, 
            double.NegativeInfinity, 
            double.PositiveInfinity, 
            Exclude.None, 
            Order.Descending);

        var plugins = new List<Plugin>();

        foreach (var id in pluginIds)
        {
            try
            {
                var pluginId = PluginId.From(id.ToString());
                var plugin = await GetAsync(pluginId, cancellationToken);
                if (plugin != null)
                    plugins.Add(plugin);
            }
            catch
            {
                // Skip invalid IDs
            }
        }

        return plugins;
    }

    public async Task AddAsync(Plugin plugin, CancellationToken cancellationToken = default)
    {
        var key = RedisKeyHelper.Plugin(plugin.Id);
        var entries = new HashEntry[]
        {
            new("Id", plugin.Id.Value),
            new("Name", plugin.Name),
            new("Version", plugin.Version),
            new("Description", plugin.Description ?? ""),
            new("Scope", plugin.Scope.ToString()),
            new("IsEnabled", plugin.IsEnabled.ToString()),
            new("InstalledAt", plugin.InstalledAt.ToString("O")),
            new("LastUpdated", plugin.LastUpdated?.ToString("O") ?? ""),
            new("LastEnabled", plugin.LastEnabled?.ToString("O") ?? ""),
            new("LastDisabled", plugin.LastDisabled?.ToString("O") ?? "")
        };

        await SetHashAsync(key, entries);
        await Database.SortedSetAddAsync(PluginsIndexKey, plugin.Id.Value, ToUnixTimestamp(plugin.InstalledAt));
    }

    public async Task UpdateAsync(Plugin plugin, CancellationToken cancellationToken = default)
    {
        await AddAsync(plugin, cancellationToken);
    }

    public async Task DeleteAsync(PluginId id, CancellationToken cancellationToken = default)
    {
        var key = RedisKeyHelper.Plugin(id);
        await DeleteAsync(key);
        await Database.SortedSetRemoveAsync(PluginsIndexKey, id.Value);
    }

    private static double ToUnixTimestamp(DateTimeOffset dateTime)
    {
        return dateTime.ToUnixTimeSeconds();
    }
}

public interface IPluginRepository
{
    Task<Plugin?> GetAsync(PluginId id, CancellationToken cancellationToken = default);
    Task<List<Plugin>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Plugin plugin, CancellationToken cancellationToken = default);
    Task UpdateAsync(Plugin plugin, CancellationToken cancellationToken = default);
    Task DeleteAsync(PluginId id, CancellationToken cancellationToken = default);
}