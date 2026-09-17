using AppIPluginRepository = BitWrite.OcelotControl.Application.Interfaces.IPluginRepository;
using AppPlugin = BitWrite.OcelotControl.Domain.Aggregates.Plugin.Plugin;
using AppPluginId = BitWrite.OcelotControl.Domain.ValueObjects.Identity.PluginId;
using InfraIPluginRepository = BitWrite.OcelotControl.Infrastructure.Repositories.IPluginRepository;

namespace BitWrite.OcelotControl.Infrastructure.Adapters;

/// <summary>
/// Adapter that implements Application.Interfaces.IPluginRepository using Infrastructure.Repositories.IPluginRepository.
/// </summary>
public class PluginRepositoryAdapter : AppIPluginRepository
{
    private readonly InfraIPluginRepository _infraRepository;

    public PluginRepositoryAdapter(InfraIPluginRepository infraRepository)
    {
        _infraRepository = infraRepository;
    }

    public Task<AppPlugin?> GetAsync(AppPluginId id, CancellationToken cancellationToken = default)
    {
        return _infraRepository.GetAsync(id, cancellationToken);
    }

    public Task<List<AppPlugin>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return _infraRepository.GetAllAsync(cancellationToken);
    }

    public Task AddAsync(AppPlugin plugin, CancellationToken cancellationToken = default)
    {
        return _infraRepository.AddAsync(plugin, cancellationToken);
    }

    public Task UpdateAsync(AppPlugin plugin, CancellationToken cancellationToken = default)
    {
        return _infraRepository.UpdateAsync(plugin, cancellationToken);
    }

    public Task DeleteAsync(AppPluginId id, CancellationToken cancellationToken = default)
    {
        return _infraRepository.DeleteAsync(id, cancellationToken);
    }
}