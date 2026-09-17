using BitWrite.OcelotControl.Domain.Aggregates.Plugin;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;

namespace BitWrite.OcelotControl.Application.Interfaces;

public interface IPluginRepository
{
    Task<Plugin?> GetAsync(PluginId id, CancellationToken cancellationToken = default);
    Task<List<Plugin>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Plugin plugin, CancellationToken cancellationToken = default);
    Task UpdateAsync(Plugin plugin, CancellationToken cancellationToken = default);
    Task DeleteAsync(PluginId id, CancellationToken cancellationToken = default);
}