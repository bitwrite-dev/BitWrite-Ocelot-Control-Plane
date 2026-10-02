using System.Text.Json;
using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Application.UseCases.Runtime;
using BitWrite.OcelotControl.Domain.Aggregates.RuntimeInstance;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Domain.ValueObjects.Status;
using FluentAssertions;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace BitWrite.OcelotControl.Application.Tests.UseCases.Runtime;

/// <summary>
/// Reading gateway runtime data for the overview page.
/// </summary>
/// <remarks>
/// A gateway record is stored as a JSON string — that is what the runtime writes on
/// every beat, and what <c>RuntimeInstanceRepository</c> writes and reads. These
/// handlers asked Redis for a hash at the same key, so a real Redis answered
/// WRONGTYPE, the gateway endpoint returned 500, and the overview page had nothing
/// to show.
///
/// The mock here answers the way Redis does rather than the way the code hoped:
/// HGETALL on a string key throws.
/// </remarks>
public class GetAllGatewaysQueryHandlerTests
{
    private static readonly Guid GatewayGuid = Guid.Parse("3ba143db-8ee4-40f0-a428-3bbf941f37d2");

    private readonly Mock<IDatabase> _database = new();
    private readonly Mock<IRuntimeInstanceRepository> _instances = new();
    private readonly GetAllGatewaysQueryHandler _handler;

    public GetAllGatewaysQueryHandlerTests()
    {
        var multiplexer = new Mock<IConnectionMultiplexer>();
        multiplexer.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_database.Object);

        _database
            .Setup(d => d.HashGetAllAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisServerException(
                "WRONGTYPE Operation against a key holding the wrong kind of value"));

        _database
            .Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(HeartbeatJson());

        _database
            .Setup(d => d.SetMembersAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync([]);

        _instances
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([Registered()]);

        _handler = new GetAllGatewaysQueryHandler(_instances.Object, multiplexer.Object);
    }

    private static RuntimeInstance Registered() =>
        RuntimeInstance.Register(
            GatewayId.From(GatewayGuid),
            new[] { "Config", "Routes" },
            string.Empty);

    private static RedisValue HeartbeatJson() =>
        JsonSerializer.Serialize(new
        {
            GatewayId = GatewayGuid.ToString(),
            Version = 4,
            Timestamp = "2026-10-01T23:39:08.4389277+00:00",
        });

    [Fact]
    public async Task ReadsRuntimeInfoFromTheStoredDocument()
    {
        var result = await _handler.HandleAsync(new GetAllGatewaysQuery());

        result.Gateways.Should().ContainSingle();
        result.Gateways[0].RuntimeInfo.Should().ContainKey("Version");
    }

    [Fact]
    public async Task ReportsTheGatewayRatherThanFailingTheWholeList()
    {
        // One unreadable gateway must not cost the overview every other gateway.
        var act = () => _handler.HandleAsync(new GetAllGatewaysQuery());

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task TreatsAGatewayThatHasNeverReportedAsHavingNoRuntimeInfo()
    {
        _database
            .Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        var result = await _handler.HandleAsync(new GetAllGatewaysQuery());

        result.Gateways.Should().ContainSingle();
        result.Gateways[0].RuntimeInfo.Should().BeEmpty();
    }

    [Fact]
    public async Task DoesNotReadTheGatewayRecordAsAHash()
    {
        // The regression in one line: the record is a string, and HGETALL on a
        // string key is an error, not an empty result.
        var hashReads = 0;
        _database
            .Setup(d => d.HashGetAllAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .Callback(() => hashReads++)
            .ThrowsAsync(new RedisServerException("WRONGTYPE"));

        await _handler.HandleAsync(new GetAllGatewaysQuery());

        hashReads.Should().Be(0);
    }
}