using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Application.UseCases.Plugin;
using DomainPlugin = BitWrite.OcelotControl.Domain.Aggregates.Plugin.Plugin;
using BitWrite.OcelotControl.Domain.Events;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Domain.ValueObjects.Status;

namespace BitWrite.OcelotControl.Application.UseCases.Plugin;

public class InstallPluginCommandHandler
{
    private readonly IPluginRepository _pluginRepository;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public InstallPluginCommandHandler(
        IPluginRepository pluginRepository,
        IDomainEventDispatcher eventDispatcher)
    {
        _pluginRepository = pluginRepository;
        _eventDispatcher = eventDispatcher;
    }

    public async Task<PluginResponse> HandleAsync(InstallPluginCommand command, CancellationToken cancellationToken = default)
    {
        // Check if plugin already exists
        var pluginId = PluginId.From(command.Id);
        var existingPlugin = await _pluginRepository.GetAsync(pluginId, cancellationToken);
        if (existingPlugin != null)
        {
            throw new InvalidOperationException($"Plugin with ID {command.Id} already exists");
        }

        // Parse scope
        var scope = PluginScope.From(command.Scope);

        // Create Plugin aggregate
        var plugin = DomainPlugin.Install(
            command.Id,
            command.Name,
            command.Version,
            command.Description,
            scope
        );

        // Persist
        await _pluginRepository.AddAsync(plugin, cancellationToken);

        // Dispatch domain events
        foreach (var domainEvent in plugin.DomainEvents)
        {
            await _eventDispatcher.DispatchAsync(domainEvent, cancellationToken);
        }
        plugin.ClearDomainEvents();

        // Dispatch audit event
        var auditEvent = new AuditRecorded(
            command.InitiatedBy,
            "InstallPlugin",
            "Plugin",
            plugin.Id.Value,
            "Success"
        );
        await _eventDispatcher.DispatchAsync(auditEvent, cancellationToken);

        return MapToResponse(plugin);
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