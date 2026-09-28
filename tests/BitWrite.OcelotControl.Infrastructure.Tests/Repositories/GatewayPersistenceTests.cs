using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Domain.Aggregates.Gateway;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Domain.ValueObjects.Status;
using BitWrite.OcelotControl.Infrastructure.Repositories;
using FluentAssertions;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace BitWrite.OcelotControl.Infrastructure.Tests.Repositories;

/// <summary>
/// Gateways in Redis.
///
/// The aggregate has no identity-preserving read path other than Reconstitute,
/// and getting that wrong is invisible until a save: a read that mints a new id
/// returns a gateway the caller believes is the one it asked for, and the write
/// that follows lands under a different key.
/// </summary>
public class GatewayPersistenceTests
{
    private readonly Mock<IDatabase> _db = new();
    private readonly Dictionary<string, HashEntry[]> _store = new(StringComparer.Ordinal);

    public GatewayPersistenceTests()
    {
        _db.Setup(d => d.HashSetAsync(
                It.IsAny<RedisKey>(), It.IsAny<HashEntry[]>(), It.IsAny<CommandFlags>()))
            .Returns<RedisKey, HashEntry[], CommandFlags>((key, entries, _) =>
            {
                _store[key.ToString()!] = entries;
                return Task.CompletedTask;
            });

        _db.Setup(d => d.HashGetAllAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .Returns<RedisKey, CommandFlags>((key, _) =>
                Task.FromResult(_store.TryGetValue(key.ToString()!, out var entries)
                    ? entries
                    : Array.Empty<HashEntry>()));

        _db.Setup(d => d.SetAddAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);
    }

    private IGatewayRepository NewGateways()
    {
        var mux = new Mock<IConnectionMultiplexer>();
        mux.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_db.Object);
        return new RedisGatewayRepository(mux.Object);
    }

    [Fact]
    public async Task ReadingKeepsTheIdentityItWasAskedFor()
    {
        // The bug: the read minted a new id, so the gateway that came back was a
        // different one from the row on disk.
        var repository = NewGateways();
        var original = Gateway.Register("edge-eu", "EU edge");
        await repository.AddAsync(original);

        var loaded = await repository.GetAsync(original.Id);

        loaded.Should().NotBeNull();
        loaded!.Id.Should().Be(original.Id);
        loaded.Name.Should().Be("edge-eu");
        loaded.Description.Should().Be("EU edge");
    }

    [Fact]
    public async Task AnUpdateRewritesTheSameRowRatherThanAddingOne()
    {
        // The bug's visible symptom: the write landed under a fresh key, so the
        // next read of the original reported the gateway as missing, and every
        // save left another duplicate behind.
        var repository = NewGateways();
        var original = Gateway.Register("edge-eu", "EU edge");
        await repository.AddAsync(original);

        var loaded = await repository.GetAsync(original.Id);
        loaded!.UpdateName("edge-eu-2");
        await repository.UpdateAsync(loaded);

        var reloaded = await repository.GetAsync(original.Id);
        reloaded.Should().NotBeNull("the update must not have moved the row");
        reloaded!.Name.Should().Be("edge-eu-2");

        _store.Keys.Should().ContainSingle("each save must not add a key");
    }

    [Fact]
    public async Task AnUpdatePreservesTheCreationTimestamp()
    {
        var repository = NewGateways();
        var original = Gateway.Register("edge-eu");
        await repository.AddAsync(original);

        var loaded = await repository.GetAsync(original.Id);
        loaded!.UpdateName("renamed");
        await repository.UpdateAsync(loaded);

        var reloaded = await repository.GetAsync(original.Id);
        reloaded!.CreatedAt.Should().BeCloseTo(original.CreatedAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task AStoredStatusIsRestored()
    {
        // The status was written on every save and silently dropped on every read,
        // so it always came back as whatever the default was.
        var repository = NewGateways();
        var original = Gateway.Register("edge-eu");
        original.SetStatus(RuntimeStatus.Degraded);
        await repository.AddAsync(original);

        var loaded = await repository.GetAsync(original.Id);

        loaded!.Status.Should().Be(RuntimeStatus.Degraded);
    }

    [Fact]
    public async Task AStoredStatusThisBuildDoesNotKnowDegradesRatherThanFailing()
    {
        var repository = NewGateways();
        var original = Gateway.Register("edge-eu");
        await repository.AddAsync(original);

        var entries = _store[$"ocelot:gateway:{original.Id.Value}"].ToList();
        _store[$"ocelot:gateway:{original.Id.Value}"] = entries
            .Select(entry => entry.Name == "Status"
                ? new HashEntry(entry.Name, "SomeRetiredStatus")
                : entry)
            .ToArray();

        // A value this build cannot parse must not make the whole gateway
        // unreadable, or one stale row would break the list.
        var loaded = await repository.GetAsync(original.Id);

        loaded.Should().NotBeNull();
        loaded!.Status.Should().Be(RuntimeStatus.Disconnected);
    }

    [Fact]
    public async Task AStoredNullDescriptionComesBackAsNullRatherThanEmpty()
    {
        var repository = NewGateways();
        var original = Gateway.Register("edge-eu");
        await repository.AddAsync(original);

        var loaded = await repository.GetAsync(original.Id);

        loaded!.Description.Should().BeNull("no description is not the same as an empty one");
    }

    [Fact]
    public async Task AnAbsentGatewayIsReportedAsNull()
    {
        var repository = NewGateways();

        var loaded = await repository.GetAsync(GatewayId.New());

        loaded.Should().BeNull();
    }
}
