using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Domain.ValueObjects.Status;
using BitWrite.OcelotControl.Infrastructure.Repositories;
using FluentAssertions;
using Moq;
using StackExchange.Redis;
using Xunit;


using BitWrite.OcelotControl.Infrastructure.Tests.Repositories;

namespace BitWrite.OcelotControl.Infrastructure.Tests.Repositories;

/// <summary>
/// Reading a publication back out of storage.
/// </summary>
/// <remarks>
/// A failed publication stores no failing gateway, so <c>FailureGatewayId</c> is an
/// empty string. Restoring one called <c>Guid.Parse("")</c> on it, which threw, and the
/// caller caught that in a bare <c>catch</c> — so the publication vanished. The list
/// reported none, <c>/current</c> reached into nothing, and the overview page failed
/// with a complaint about target gateways.
///
/// The list being empty is the symptom; the failure being silent is the defect.
/// </remarks>
public class RedisPublicationRepositoryTests
{
    private const string IndexKey = "ocelot:index:publications";
    private const string PublicationId = "2b8f95f3-b33b-4e1e-9508-727e3b8f3893";
    private const string FailureGatewayId = "3bc9a0f8-3334-4454-822f-474b6d13e036";

    private readonly Mock<IDatabase> _database = new();
    private readonly RedisPublicationRepository _repository;
    private readonly Dictionary<string, HashEntry[]> _stored = new(StringComparer.Ordinal);

    public RedisPublicationRepositoryTests()
    {
        var multiplexer = new Mock<IConnectionMultiplexer>();
        multiplexer.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_database.Object);

        // Match every overload: which one is chosen depends on the call, and a mock
        // set up for the wrong one silently answers "no entries".
        _database
            .Setup(d => d.SortedSetRangeByScoreAsync(
                It.IsAny<RedisKey>(), It.IsAny<double>(), It.IsAny<double>(),
                It.IsAny<Exclude>(), It.IsAny<Order>(), It.IsAny<long>(), It.IsAny<long>(),
                It.IsAny<CommandFlags>()))
            // A callback, not a value: `ReturnsAsync(value)` is evaluated when the
            // mock is set up, which is before any test has stored anything, so it
            // answered "no entries" for every test in the class.
            .Returns(() => Task.FromResult(StoredIds()));

        _database
            .Setup(d => d.HashGetAllAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((RedisKey key, CommandFlags _) =>
                _stored.TryGetValue(key.ToString(), out var entries) ? entries : []);

        _repository = new RedisPublicationRepository(multiplexer.Object, TestEnvironment.Context());
    }

    private RedisValue[] StoredIds() =>
        _stored.Keys
            .Select(key => (RedisValue)key["ocelot:publication:".Length..])
            .ToArray();

    private static HashEntry[] Entry(string status, string failureGatewayId = "") =>
    [
        new("Id", PublicationId),
        new("SnapshotVersion", "2"),
        new("Status", status),
        new("InitiatedBy", "dashboard"),
        new("StartedAt", "2026-09-28T21:27:30.5962919+00:00"),
        new("CompletedAt", ""),
        new("FailureReason", "gateway refused the configuration"),
        new("FailureGatewayId", failureGatewayId),
    ];

    [Fact]
    public async Task RestoresAFailedPublicationThatNamedNoGateway()
    {
        // The state a failed publication is actually stored in: it failed, and it
        // failed without a gateway to blame.
        _stored[$"ocelot:publication:{PublicationId}"] = Entry("Failed");

        var publication = await _repository.GetLatestAsync();

        publication.Should().NotBeNull();
        publication!.Status.Should().Be(PublicationStatus.Failed);
        publication.FailureReason.Should().Be("gateway refused the configuration");
    }

    [Fact]
    public async Task RestoresAFailedPublicationThatNamedAGateway()
    {
        _stored[$"ocelot:publication:{PublicationId}"] = Entry("Failed", FailureGatewayId);

        var publication = await _repository.GetLatestAsync();

        publication.Should().NotBeNull();
        publication!.Status.Should().Be(PublicationStatus.Failed);
    }

    [Fact]
    public async Task RestoresAPublishedOne()
    {
        _stored[$"ocelot:publication:{PublicationId}"] = Entry("Published");

        var publication = await _repository.GetLatestAsync();

        publication.Should().NotBeNull();
        publication!.SnapshotVersion.Should().Be(SnapshotVersion.From(2));
    }

    [Fact]
    public async Task KeepsAFailedPublicationInTheListRatherThanDroppingIt()
    {
        // The symptom as it was seen: the list reported zero while the index held
        // four ids, because each one threw on the way out and the catch was silent.
        _stored[$"ocelot:publication:{PublicationId}"] = Entry("Failed");

        var all = await _repository.GetAllAsync();

        all.Should().ContainSingle();
        all[0].Status.Should().Be(PublicationStatus.Failed);
    }

    [Fact]
    public async Task ReportsNothingWhenNothingHasBeenStored()
    {
        (await _repository.GetLatestAsync()).Should().BeNull();
        (await _repository.GetAllAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task RestoresARolledBackOne()
    {
        _stored[$"ocelot:publication:{PublicationId}"] =
        [
            new("Id", PublicationId),
            new("SnapshotVersion", "2"),
            new("Status", "RolledBack"),
            new("InitiatedBy", "dashboard"),
            new("StartedAt", "2026-09-28T21:27:30.5962919+00:00"),
            new("CompletedAt", ""),
            new("FailureReason", ""),
            new("FailureGatewayId", ""),
            new("TargetVersion", "1"),
        ];

        var publication = await _repository.GetLatestAsync();

        publication.Should().NotBeNull();
        publication!.Status.Should().Be(PublicationStatus.RolledBack);
    }
}