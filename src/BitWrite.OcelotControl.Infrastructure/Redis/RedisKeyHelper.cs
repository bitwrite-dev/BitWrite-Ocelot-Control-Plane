using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using StackExchange.Redis;
using System.Text.Json;

namespace BitWrite.OcelotControl.Infrastructure.Redis;

public static class RedisKeyHelper
{
    public const string GlobalConfig = "ocelot:global";
    public const string RuntimeCurrent = "ocelot:runtime:current";
    public const string LockConfiguration = "ocelot:lock:configuration";

    public static string Gateway(GatewayId id) => $"ocelot:gateway:{id.Value}";
    public static string Service(ServiceId id) => $"ocelot:service:{id.Value}";
    public static string Route(RouteId id) => $"ocelot:route:{id.Value}";
    public static string Snapshot(SnapshotVersion version) => $"ocelot:snapshot:{version.Value}";
    public static string Publication(PublicationId id) => $"ocelot:publication:{id.Value}";
    public static string Plugin(PluginId id) => $"ocelot:plugin:{id.Value}";
    public static string RuntimeInstance(GatewayId id) => $"ocelot:runtime:gateway:{id.Value}";

    public const string IndexServices = "ocelot:index:services";
    public const string IndexRoutes = "ocelot:index:routes";
    public const string IndexSnapshots = "ocelot:index:snapshots";
    
    public static string IndexServiceRoutes(ServiceId serviceId) => $"ocelot:index:service:{serviceId.Value}:routes";
    public static string IndexRouteSignature(string signature) => $"ocelot:index:route-signature:{signature}";
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