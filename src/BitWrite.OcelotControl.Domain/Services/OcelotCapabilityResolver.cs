using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.Status;

namespace BitWrite.OcelotControl.Domain.Services;

/// <summary>
/// Resolves Ocelot capabilities based on version (§57).
/// Determines which features are available for a given Ocelot version.
/// </summary>
public class OcelotCapabilityResolver
{
    private readonly Dictionary<string, CapabilityDefinition> _capabilities;

    public OcelotCapabilityResolver()
    {
        _capabilities = InitializeCapabilities();
    }

    /// <summary>
    /// Checks if a capability is supported for the given Ocelot version.
    /// </summary>
    public bool IsCapabilitySupported(string capabilityKey, OcelotVersion ocelotVersion)
    {
        if (!_capabilities.TryGetValue(capabilityKey, out var capability))
            return false;

        return ocelotVersion.IsAtLeast(capability.MinimumVersion);
    }

    /// <summary>
    /// Gets all supported capabilities for the given Ocelot version.
    /// </summary>
    public IReadOnlyList<CapabilityKey> GetSupportedCapabilities(OcelotVersion ocelotVersion)
    {
        return _capabilities
            .Where(c => ocelotVersion.IsAtLeast(c.Value.MinimumVersion))
            .Select(c => CapabilityKey.From(c.Key))
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Gets capability definition for a specific capability key.
    /// </summary>
    public CapabilityDefinition? GetCapabilityDefinition(string capabilityKey)
    {
        return _capabilities.TryGetValue(capabilityKey, out var capability) ? capability : null;
    }

    /// <summary>
    /// Resolves the effective capabilities for a gateway based on its Ocelot version and enabled plugins.
    /// </summary>
    public IReadOnlyList<CapabilityKey> ResolveEffectiveCapabilities(
        OcelotVersion ocelotVersion,
        IReadOnlyList<string> enabledPluginCapabilities)
    {
        var capabilities = new List<CapabilityKey>();

        // Add built-in capabilities
        capabilities.AddRange(GetSupportedCapabilities(ocelotVersion));

        // Add plugin capabilities
        foreach (var pluginCapability in enabledPluginCapabilities)
        {
            if (!capabilities.Any(c => c.Value == pluginCapability))
            {
                capabilities.Add(CapabilityKey.From(pluginCapability));
            }
        }

        return capabilities.AsReadOnly();
    }

    private Dictionary<string, CapabilityDefinition> InitializeCapabilities()
    {
        return new Dictionary<string, CapabilityDefinition>
        {
            // Core capabilities (v18.0+)
            ["http"] = new CapabilityDefinition("http", "HTTP Routes", OcelotVersion.V18_0, CapabilityScope.Route),
            ["https"] = new CapabilityDefinition("https", "HTTPS Routes", OcelotVersion.V18_0, CapabilityScope.Route),
            ["authentication"] = new CapabilityDefinition("authentication", "Authentication", OcelotVersion.V18_0, CapabilityScope.Route),
            ["authorization"] = new CapabilityDefinition("authorization", "Authorization", OcelotVersion.V18_0, CapabilityScope.Route),
            ["rate-limiting"] = new CapabilityDefinition("rate-limiting", "Rate Limiting", OcelotVersion.V18_0, CapabilityScope.Route),
            ["qos"] = new CapabilityDefinition("qos", "Quality of Service", OcelotVersion.V18_0, CapabilityScope.Route),
            ["caching"] = new CapabilityDefinition("caching", "Response Caching", OcelotVersion.V18_0, CapabilityScope.Route),
            ["load-balancing"] = new CapabilityDefinition("load-balancing", "Load Balancing", OcelotVersion.V18_0, CapabilityScope.Route),
            ["header-transformation"] = new CapabilityDefinition("header-transformation", "Header Transformation", OcelotVersion.V18_0, CapabilityScope.Route),
            ["claim-transformation"] = new CapabilityDefinition("claim-transformation", "Claim Transformation", OcelotVersion.V18_0, CapabilityScope.Route),
            ["query-string-transformation"] = new CapabilityDefinition("query-string-transformation", "Query String Transformation", OcelotVersion.V18_0, CapabilityScope.Route),

            // Advanced capabilities (v19.0+)
            ["grpc"] = new CapabilityDefinition("grpc", "gRPC Routes", OcelotVersion.V19_0, CapabilityScope.Route),
            ["websocket"] = new CapabilityDefinition("websocket", "WebSocket Support", OcelotVersion.V19_0, CapabilityScope.Route),
            ["aggregation"] = new CapabilityDefinition("aggregation", "Request Aggregation", OcelotVersion.V19_0, CapabilityScope.Route),

            // Dynamic capabilities (v20.0+)
            ["dynamic-routes"] = new CapabilityDefinition("dynamic-routes", "Dynamic Routes", OcelotVersion.V20_0, CapabilityScope.Gateway),

            // Plugin capabilities (version varies)
            ["plugin-authentication"] = new CapabilityDefinition("plugin-authentication", "Plugin Authentication", OcelotVersion.V18_0, CapabilityScope.Route),
            ["plugin-authorization"] = new CapabilityDefinition("plugin-authorization", "Plugin Authorization", OcelotVersion.V18_0, CapabilityScope.Route),
            ["plugin-rate-limiting"] = new CapabilityDefinition("plugin-rate-limiting", "Plugin Rate Limiting", OcelotVersion.V18_0, CapabilityScope.Route),
        };
    }
}

public class CapabilityDefinition
{
    public string Key { get; }
    public string Name { get; }
    public OcelotVersion MinimumVersion { get; }
    public CapabilityScope Scope { get; }

    public CapabilityDefinition(string key, string name, OcelotVersion minimumVersion, CapabilityScope scope)
    {
        Key = key;
        Name = name;
        MinimumVersion = minimumVersion;
        Scope = scope;
    }
}

public enum CapabilityScope
{
    Route,
    Gateway,
    Global
}