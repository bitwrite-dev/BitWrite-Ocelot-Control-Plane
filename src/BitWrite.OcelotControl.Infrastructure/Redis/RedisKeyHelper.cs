using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using StackExchange.Redis;
using System.Text.Json;

namespace BitWrite.OcelotControl.Infrastructure.Redis;

public static class RedisKeyHelper
{
    public const string GlobalConfig = "ocelot:global";
    /// <summary>The one system settings row, holding the Ocelot version.</summary>
    public const string SystemSettings = "ocelot:settings";
    public static string RuntimeCurrent(EnvironmentName environment) => $"ocelot:runtime:current:{environment.Value}";
    /// <summary>
    /// One entry per configuration a gateway tried to apply, appended by the runtime.
    /// </summary>
    /// <remarks>
    /// Unbounded, and nothing trimmed it until something read it. It is the only
    /// record of what gateways actually experienced when applying a configuration.
    /// </remarks>
    public const string GatewayActivationResults = "ocelot:gateway:activation:results";
    public const string LockConfiguration = "ocelot:lock:configuration";

    public static string Gateway(GatewayId id) => $"ocelot:gateway:{id.Value}";
    public static string Service(ServiceId id, EnvironmentName environment) => $"ocelot:service:{environment.Value}:{id.Value}";
    public static string Route(RouteId id, EnvironmentName environment) => $"ocelot:route:{environment.Value}:{id.Value}";
    public static string Snapshot(SnapshotVersion version, EnvironmentName environment) => $"ocelot:snapshot:{environment.Value}:{version.Value}";
    public static string Publication(PublicationId id) => $"ocelot:publication:{id.Value}";
    public static string Plugin(PluginId id) => $"ocelot:plugin:{id.Value}";
    public static string RuntimeInstance(GatewayId id) => $"ocelot:runtime:gateway:{id.Value}";
    /// <summary>
    /// What the runtime last reported, on its own key.
    /// </summary>
    /// <remarks>
    /// Separate from the instance record because the two are written by different
    /// parties and answer different questions. The runtime cannot report a status —
    /// only the control plane knows what it asked for — so when both wrote one key,
    /// every heartbeat replaced the status with nothing and the gateway went back to
    /// looking disconnected.
    /// </remarks>
    public static string RuntimeGatewayHeartbeat(GatewayId id) => $"{RuntimeInstance(id)}:heartbeat";
    public static string License(LicenseId id) => $"ocelot:license:{id.Value}";
    public static string AuditLog(string id) => $"ocelot:audit:{id}";

    public const string IndexLicenses = "ocelot:index:licenses";
    public const string IndexAuditLogs = "ocelot:index:audit-logs";

    // The indexes of the environment-scoped collections carry the environment too.
    //
    // Leaving them shared would leave the isolation half-applied and would break in
    // the way a missing row does not: every environment's id lands in one set, so
    // deleting a route in development would remove a production route from the index
    // and leave its row unreachable rather than deleted — and a list would read the
    // shared set, drop every id absent from its own environment, and answer with a
    // short list that looks like the whole truth.
    public static string IndexServices(EnvironmentName environment) => $"ocelot:index:services:{environment.Value}";
    public static string IndexRoutes(EnvironmentName environment) => $"ocelot:index:routes:{environment.Value}";
    public static string IndexSnapshots(EnvironmentName environment) => $"ocelot:index:snapshots:{environment.Value}";
    public static string IndexServiceRoutes(ServiceId serviceId, EnvironmentName environment) =>
        $"ocelot:index:service:{environment.Value}:{serviceId.Value}:routes";
    public static string IndexRouteSignature(string signature, EnvironmentName environment) =>
        $"ocelot:index:route-signature:{environment.Value}:{signature}";
    public static string IndexLicenseProductCodes = "ocelot:index:license-product-codes";
}

public static class RedisSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);
    
    public static T? Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, Options);
    
    public static HashEntry[] ToHashEntries(object obj)
    {
        var properties = obj.GetType().GetProperties();
        var entries = new List<HashEntry>();
        
        foreach (var prop in properties)
        {
            var value = prop.GetValue(obj);
            if (value != null)
            {
                var json = Serialize(value);
                entries.Add(new HashEntry(prop.Name, json));
            }
        }
        
        return entries.ToArray();
    }
    
    public static T FromHashEntries<T>(HashEntry[] entries) where T : new()
    {
        var obj = new T();
        var properties = typeof(T).GetProperties()
            .ToDictionary(p => p.Name, p => p);
        
        foreach (var entry in entries)
        {
            if (properties.TryGetValue(entry.Name, out var prop))
            {
                var value = Deserialize(prop.PropertyType, entry.Value.ToString());
                prop.SetValue(obj, value);
            }
        }
        
        return obj;
    }
    
    private static object? Deserialize(Type type, string json)
    {
        return JsonSerializer.Deserialize(json, type, Options);
    }
}