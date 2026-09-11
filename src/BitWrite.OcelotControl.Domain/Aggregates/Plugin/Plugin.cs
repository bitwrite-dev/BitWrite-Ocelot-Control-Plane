using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Domain.ValueObjects.Status;
using BitWrite.OcelotControl.Domain.Events;
using BitWrite.OcelotControl.Domain.Exceptions;
using BitWrite.OcelotControl.Domain.Services;

namespace BitWrite.OcelotControl.Domain.Aggregates.Plugin;

/// <summary>
/// Plugin Aggregate Root - Plugin Metadata & Lifecycle (§9A.2)
/// Represents an Ocelot plugin with its metadata and lifecycle state.
/// </summary>
public class Plugin
{
    private readonly List<DomainEvent> _domainEvents = new();
    private readonly List<PluginCapability> _capabilities = new();

    public PluginId Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Version { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public PluginScope Scope { get; private set; } = PluginScope.Global;
    public bool IsEnabled { get; private set; } = true;
    public DateTimeOffset InstalledAt { get; private set; }
    public DateTimeOffset? LastUpdated { get; private set; }
    public DateTimeOffset? LastEnabled { get; private set; }
    public DateTimeOffset? LastDisabled { get; private set; }

    public IReadOnlyList<PluginCapability> Capabilities => _capabilities.AsReadOnly();
    public IReadOnlyList<DomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    private Plugin() { }

    /// <summary>
    /// Factory method to install a new plugin.
    /// </summary>
    public static Plugin Install(
        string id,
        string name,
        string version,
        string? description = null,
        PluginScope? scope = null,
        IReadOnlyList<PluginCapability>? capabilities = null,
        string correlationId = "")
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new DomainException("Plugin ID cannot be empty", "INVALID_PLUGIN_ID");

        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Plugin name cannot be empty", "INVALID_PLUGIN_NAME");

        if (string.IsNullOrWhiteSpace(version))
            throw new DomainException("Plugin version cannot be empty", "INVALID_PLUGIN_VERSION");

        var plugin = new Plugin
        {
            Id = PluginId.From(id),
            Name = name.Trim(),
            Version = version.Trim(),
            Description = description?.Trim(),
            Scope = scope ?? PluginScope.Global,
            IsEnabled = true,
            InstalledAt = DateTimeOffset.UtcNow
        };

        if (capabilities != null)
        {
            plugin._capabilities.AddRange(capabilities);
        }

        plugin.AddDomainEvent(new PluginInstalled(plugin.Id, plugin.Version));
        return plugin;
    }

    /// <summary>
    /// Enables the plugin.
    /// </summary>
    public void Enable(string correlationId = "")
    {
        if (IsEnabled)
            return;

        IsEnabled = true;
        LastEnabled = DateTimeOffset.UtcNow;
        LastUpdated = DateTimeOffset.UtcNow;

        AddDomainEvent(new PluginEnabled(Id));
    }

    /// <summary>
    /// Disables the plugin.
    /// </summary>
    public void Disable(string correlationId = "")
    {
        if (!IsEnabled)
            return;

        IsEnabled = false;
        LastDisabled = DateTimeOffset.UtcNow;
        LastUpdated = DateTimeOffset.UtcNow;

        AddDomainEvent(new PluginDisabled(Id));
    }

    /// <summary>
    /// Upgrades the plugin to a new version.
    /// </summary>
    public void Upgrade(string newVersion, string correlationId = "")
    {
        if (string.IsNullOrWhiteSpace(newVersion))
            throw new DomainException("New version cannot be empty", "INVALID_VERSION");

        var oldVersion = Version;
        Version = newVersion.Trim();
        LastUpdated = DateTimeOffset.UtcNow;

        AddDomainEvent(new PluginUpgraded(Id, oldVersion, newVersion));
    }

    /// <summary>
    /// Uninstalls the plugin (soft delete).
    /// </summary>
    public void Uninstall(string correlationId = "")
    {
        IsEnabled = false;
        LastUpdated = DateTimeOffset.UtcNow;

        AddDomainEvent(new PluginUninstalled(Id));
    }

    /// <summary>
    /// Adds a capability to the plugin.
    /// </summary>
    public void AddCapability(PluginCapability capability)
    {
        if (_capabilities.Any(c => c.Key == capability.Key))
            throw new DomainException($"Capability {capability.Key} already exists", "DUPLICATE_CAPABILITY");

        _capabilities.Add(capability);
        LastUpdated = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Removes a capability from the plugin.
    /// </summary>
    public void RemoveCapability(string capabilityKey)
    {
        var capability = _capabilities.FirstOrDefault(c => c.Key == capabilityKey);
        if (capability != null)
        {
            _capabilities.Remove(capability);
            LastUpdated = DateTimeOffset.UtcNow;
        }
    }

    /// <summary>
    /// Checks if plugin has a specific capability.
    /// </summary>
    public bool HasCapability(string capabilityKey)
    {
        return _capabilities.Any(c => c.Key == capabilityKey);
    }

    /// <summary>
    /// Gets all capability keys.
    /// </summary>
    public IReadOnlyList<string> GetCapabilityKeys()
    {
        return _capabilities.Select(c => c.Key).ToList().AsReadOnly();
    }

    private void AddDomainEvent(DomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}

/// <summary>
/// Plugin capability entity.
/// </summary>
public class PluginCapability
{
    public string Key { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public CapabilityScope Scope { get; init; } = CapabilityScope.Route;
}