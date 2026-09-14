using System.Text.Json;
using System.Reflection;
using BitWrite.OcelotControl.Domain.Events;

namespace BitWrite.OcelotControl.Infrastructure.Outbox;

public interface IEventSerializer
{
    string Serialize(DomainEvent domainEvent);
    DomainEvent? Deserialize(string json, string eventType);
}

public class JsonEventSerializer : IEventSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private static readonly Dictionary<string, Type> EventTypeRegistry = InitializeEventTypeRegistry();

    private static Dictionary<string, Type> InitializeEventTypeRegistry()
    {
        var registry = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
        
        // Get DomainEvent types from the Domain assembly
        var domainAssembly = typeof(DomainEvent).Assembly;
        var domainEventTypes = domainAssembly.GetTypes()
            .Where(t => typeof(DomainEvent).IsAssignableFrom(t) && t != typeof(DomainEvent))
            .ToList();

        foreach (var type in domainEventTypes)
        {
            registry[type.Name] = type;
        }

        return registry;
    }

    public string Serialize(DomainEvent domainEvent)
    {
        var envelope = new EventEnvelope
        {
            EventType = domainEvent.GetType().Name,
            Version = "1.0",
            OccurredAt = domainEvent.OccurredAt,
            CorrelationId = domainEvent.CorrelationId,
            Payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), Options)
        };

        return JsonSerializer.Serialize(envelope, Options);
    }

    public DomainEvent? Deserialize(string json, string eventTypeHint)
    {
        var envelope = JsonSerializer.Deserialize<EventEnvelope>(json, Options);
        if (envelope == null) return null;

        // Use the eventType from the envelope if available, otherwise use provided hint
        var typeName = !string.IsNullOrEmpty(envelope.EventType) ? envelope.EventType : eventTypeHint;

        if (!EventTypeRegistry.TryGetValue(typeName, out var eventType))
        {
            // Try to find by suffix matching
            eventType = FindEventType(typeName);
        }

        if (eventType == null)
        {
            return null;
        }

        try
        {
            var domainEvent = JsonSerializer.Deserialize(envelope.Payload, eventType, Options);
            return domainEvent as DomainEvent;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private Type? FindEventType(string typeName)
    {
        // Try exact match first
        if (EventTypeRegistry.TryGetValue(typeName, out var exact))
            return exact;

        // Try suffix matching for common patterns
        var suffixes = new[] { "Event", "IntegrationEvent" };
        foreach (var suffix in suffixes)
        {
            if (typeName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                var baseName = typeName[..^suffix.Length];
                if (EventTypeRegistry.TryGetValue(baseName, out var found))
                    return found;
            }
        }

        return null;
    }
}

public class EventEnvelope
{
    public string EventType { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
}