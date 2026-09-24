using BitWrite.OcelotControl.Infrastructure.Outbox;
using BitWrite.OcelotControl.Domain.Events;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using FluentAssertions;
using Xunit;

namespace BitWrite.OcelotControl.Infrastructure.Tests.Outbox;

public class JsonEventSerializerTests
{
    private readonly IEventSerializer _serializer;

    public JsonEventSerializerTests()
    {
        _serializer = new JsonEventSerializer();
    }

    [Fact]
    public void Serialize_ShouldReturnValidJson()
    {
        var gatewayId = GatewayId.New();
        var domainEvent = new GatewayRegistered(gatewayId, "Test Gateway", "Description");

        var json = _serializer.Serialize(domainEvent);

        json.Should().NotBeNullOrEmpty();
        json.Should().Contain("GatewayRegistered");
        json.Should().Contain("1.0");
    }

    [Fact]
    public void Serialize_ShouldContainEnvelopeStructure()
    {
        var gatewayId = GatewayId.New();
        var domainEvent = new GatewayRegistered(gatewayId, "Test Gateway", "Description");

        var json = _serializer.Serialize(domainEvent);

        json.Should().Contain("eventType");
        json.Should().Contain("version");
        json.Should().Contain("occurredAt");
        json.Should().Contain("correlationId");
        json.Should().Contain("payload");
    }
}