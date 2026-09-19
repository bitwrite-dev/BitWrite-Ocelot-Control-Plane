using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Application.UseCases.Plugin;
using DomainPlugin = BitWrite.OcelotControl.Domain.Aggregates.Plugin.Plugin;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;

namespace BitWrite.OcelotControl.Application.UseCases.Plugin;

public class GetPluginQueryHandler
{
    private readonly IPluginRepository _pluginRepository;

    public GetPluginQueryHandler(IPluginRepository pluginRepository)
    {
        _pluginRepository = pluginRepository;
    }

    public async Task<PluginResponse?> HandleAsync(GetPluginQuery query, CancellationToken cancellationToken = default)
    {
        var plugin = await _pluginRepository.GetAsync(query.Id, cancellationToken);
        return plugin != null ? MapToResponse(plugin) : null;
    }

    private static PluginResponse MapToResponse(DomainPlugin plugin)
    {
        return new PluginResponse(
            plugin.Id,
            plugin.Name,
            plugin.Version,
            plugin.Description,
            plugin.Scope,
            plugin.IsEnabled,
            plugin.InstalledAt,
            plugin.LastUpdated,
            plugin.LastEnabled,
            plugin.LastDisabled,
            plugin.GetCapabilityKeys()
        );
    }
}