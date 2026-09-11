using BitWrite.OcelotControl.Infrastructure.Outbox;
using BitWrite.OcelotControl.Domain.Events;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using FluentAssertions;
using Xunit;

namespace BitWrite.OcelotControl.Application.Tests.Events;

public class EventSerializerTests
{
    private readonly IEventSerializer _serializer;

    public EventSerializerTests()
    {
        _serializer = new JsonEventSerializer();
    }

    [Fact]
    public void Serialize_ShouldReturnValidJson()
    {
        // Arrange
        var gatewayId = GatewayId.New();
        var domainEvent = new GatewayRegistered(gatewayId, "Test Gateway", "Description");

        // Act
        var json = _serializer.Serialize(domainEvent);

        // Assert
        json.Should().NotBeNullOrEmpty();
        json.Should().Contain("GatewayRegistered");
        json.Should().Contain("1.0");
    }

    [Fact]
    public void Serialize_ShouldContainEnvelopeStructure()
    {
        // Arrange
        var gatewayId = GatewayId.New();
        var domainEvent = new GatewayRegistered(gatewayId, "Test Gateway", "Description");

        // Act
        var json = _serializer.Serialize(domainEvent);

        // Assert
        json.Should().Contain("eventType");
        json.Should().Contain("version");
        json.Should().Contain("occurredAt");
        json.Should().Contain("correlationId");
        json.Should().Contain("payload");
    }
}
