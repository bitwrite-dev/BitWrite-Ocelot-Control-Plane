using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Application.UseCases.Plugin;
using DomainPlugin = BitWrite.OcelotControl.Domain.Aggregates.Plugin.Plugin;
using BitWrite.OcelotControl.Domain.Events;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;

namespace BitWrite.OcelotControl.Application.UseCases.Plugin;

public class UninstallPluginCommandHandler
{
    private readonly IPluginRepository _pluginRepository;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public UninstallPluginCommandHandler(
        IPluginRepository pluginRepository,
        IDomainEventDispatcher eventDispatcher)
    {
        _pluginRepository = pluginRepository;
        _eventDispatcher = eventDispatcher;
    }

    public async Task<bool> HandleAsync(UninstallPluginCommand command, CancellationToken cancellationToken = default)
    {
        var plugin = await _pluginRepository.GetAsync(command.Id, cancellationToken);
        if (plugin == null)
            return false;

        // Uninstall the plugin
        plugin.Uninstall();

        // Persist
        await _pluginRepository.UpdateAsync(plugin, cancellationToken);

        // Dispatch domain events
        foreach (var domainEvent in plugin.DomainEvents)
        {
            await _eventDispatcher.DispatchAsync(domainEvent, cancellationToken);
        }
        plugin.ClearDomainEvents();

        // Dispatch audit event
        var auditEvent = new AuditRecorded(
            command.InitiatedBy,
            "UninstallPlugin",
            "Plugin",
            plugin.Id.Value,
            "Success"
        );
        await _eventDispatcher.DispatchAsync(auditEvent, cancellationToken);

        return true;
    }
}