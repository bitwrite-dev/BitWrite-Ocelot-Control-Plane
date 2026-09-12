using AppIEventSerializer = BitWrite.OcelotControl.Application.Interfaces.IEventSerializer;
using InfraIEventSerializer = BitWrite.OcelotControl.Infrastructure.Outbox.IEventSerializer;
using BitWrite.OcelotControl.Domain.Events;

namespace BitWrite.OcelotControl.Infrastructure.Adapters;

/// <summary>
/// Adapter that implements Application.Interfaces.IEventSerializer using Infrastructure.Outbox.IEventSerializer.
/// </summary>
public class EventSerializerAdapter : AppIEventSerializer
{
    private readonly InfraIEventSerializer _infraSerializer;

    public EventSerializerAdapter(InfraIEventSerializer infraSerializer)
    {
        _infraSerializer = infraSerializer;
    }

    public string Serialize(DomainEvent domainEvent)
    {
        return _infraSerializer.Serialize(domainEvent);
    }

    public DomainEvent? Deserialize(string json, string eventType)
    {
        return _infraSerializer.Deserialize(json, eventType);
    }
}