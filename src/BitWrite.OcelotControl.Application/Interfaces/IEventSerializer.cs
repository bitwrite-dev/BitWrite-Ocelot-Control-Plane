using BitWrite.OcelotControl.Domain.Events;

namespace BitWrite.OcelotControl.Application.Interfaces;

public interface IEventSerializer
{
    string Serialize(DomainEvent domainEvent);
    DomainEvent? Deserialize(string json, string eventType);
}