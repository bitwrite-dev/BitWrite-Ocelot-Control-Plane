using System.Text.Json;
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

    public DomainEvent? Deserialize(string json, string eventType)
    {
        var envelope = JsonSerializer.Deserialize<EventEnvelope>(json, Options);
        if (envelope == null) return null;

        // For now, return null as we need to implement event type resolution
        // In production, use a registry or assembly scanning
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
