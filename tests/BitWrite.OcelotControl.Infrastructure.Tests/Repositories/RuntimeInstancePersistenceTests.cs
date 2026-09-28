using IRuntimeInstanceRepository = BitWrite.OcelotControl.Infrastructure.Repositories.IRuntimeInstanceRepository;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Infrastructure.Repositories;
using FluentAssertions;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace BitWrite.OcelotControl.Infrastructure.Tests.Repositories;

/// <summary>
/// Runtime heartbeats in Redis.
///
/// The runtime writes a JSON string under the instance key. This used to be read
/// as a hash, so the two disagreed and no heartbeat was ever observed — anything
/// derived from one, such as "when did this gateway last report in", was
/// permanently empty rather than unknown.
/// </summary>
public class RuntimeInstancePersistenceTests
{
    private readonly Mock<IDatabase> _db = new();
    private readonly Dictionary<string, RedisValue> _strings = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HashEntry[]> _hashes = new(StringComparer.Ordinal);

    public RuntimeInstancePersistenceTests()
    {
        _db.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .Returns<RedisKey, CommandFlags>((key, _) =>
                Task.FromResult(
                    _strings.TryGetValue(key.ToString()!, out var value) ? value : RedisValue.Null));

        _db.Setup(d => d.StringSetAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .Returns<RedisKey, RedisValue, TimeSpan?, bool, When, CommandFlags>(
                (key, value, _, _, _, _) =>
                {
                    _strings[key.ToString()!] = value;
                    return Task.FromResult(true);
                });

        _db.Setup(d => d.HashSetAsync(
                It.IsAny<RedisKey>(), It.IsAny<HashEntry[]>(), It.IsAny<CommandFlags>()))
            .Returns<RedisKey, HashEntry[], CommandFlags>((key, entries, _) =>
            {
                _hashes[key.ToString()!] = entries;
                return Task.CompletedTask;
            });

        _db.Setup(d => d.HashGetAllAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .Returns<RedisKey, CommandFlags>((key, _) =>
                Task.FromResult(_hashes.TryGetValue(key.ToString()!, out var entries)
                    ? entries
                    : Array.Empty<HashEntry>()));
    }

    private IRuntimeInstanceRepository NewInstances()
    {
        var mux = new Mock<IConnectionMultiplexer>();
        mux.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_db.Object);
        return new RedisRuntimeInstanceRepository(mux.Object);
    }

    /// <summary>
    /// The shape the runtime itself writes, in RuntimeAdapter.UpdateHeartbeatAsync.
    /// </summary>
    private static string Heartbeat(string gatewayId, string? version, string timestamp) =>
        $$"""{"GatewayId":"{{gatewayId}}","Version":{{(version is null ? "null" : $"\"{version}\"")}},"Timestamp":"{{timestamp}}"}""";

    [Fact]
    public async Task AHeartbeatWrittenAsJsonIsRead()
    {
        // The bug: this was read as a hash, found nothing, and reported every
        // gateway as having no runtime data at all.
        var repository = NewInstances();
        var id = GatewayId.New();
        var beat = DateTimeOffset.UtcNow.AddSeconds(-30);
        _strings[$"ocelot:runtime:gateway:{id.Value}"] =
            Heartbeat(id.Value.ToString(), "7", beat.ToString("O"));

        var loaded = await repository.GetAsync(id);

        loaded.Should().NotBeNull();
        loaded!.GatewayId.Should().Be(id);
        loaded.LastHeartbeat.Should().BeCloseTo(beat, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task AHeartbeatReportsTheVersionItIsRunning()
    {
        var repository = NewInstances();
        var id = GatewayId.New();
        _strings[$"ocelot:runtime:gateway:{id.Value}"] =
            Heartbeat(id.Value.ToString(), "7", DateTimeOffset.UtcNow.ToString("O"));

        var loaded = await repository.GetAsync(id);

        loaded!.CurrentVersion.Should().Be(SnapshotVersion.From(7));
    }

    [Fact]
    public async Task AHeartbeatWithNoVersionIsReadRatherThanFailing()
    {
        // A runtime that has not applied anything yet reports a null version.
        var repository = NewInstances();
        var id = GatewayId.New();
        _strings[$"ocelot:runtime:gateway:{id.Value}"] =
            Heartbeat(id.Value.ToString(), null, DateTimeOffset.UtcNow.ToString("O"));

        var loaded = await repository.GetAsync(id);

        loaded.Should().NotBeNull();
        loaded!.CurrentVersion.Should().BeNull();
    }

    [Fact]
    public async Task AReadDoesNotReportEveryGatewayAsJustConnected()
    {
        // Register stamps the current time and sets Connecting, so reading
        // through it would report a heartbeat that had just arrived regardless of
        // when it actually did.
        var repository = NewInstances();
        var id = GatewayId.New();
        var old = DateTimeOffset.UtcNow.AddHours(-3);
        _strings[$"ocelot:runtime:gateway:{id.Value}"] =
            Heartbeat(id.Value.ToString(), "3", old.ToString("O"));

        var loaded = await repository.GetAsync(id);

        loaded!.LastHeartbeat.Should().BeCloseTo(old, TimeSpan.FromSeconds(1));
        loaded.Status.Should().NotBe(RuntimeStatusFrom("Connecting"));
    }

    [Fact]
    public async Task AStoredStatusIsKeptWhenTheHeartbeatDoesNotReportOne()
    {
        // The heartbeat payload carries no status, so a reported one stands.
        var repository = NewInstances();
        var id = GatewayId.New();
        _strings[$"ocelot:runtime:gateway:{id.Value}"] =
            Heartbeat(id.Value.ToString(), "1", DateTimeOffset.UtcNow.ToString("O"));

        var loaded = await repository.GetAsync(id);

        loaded!.Status.Should().Be(RuntimeStatusFrom("Disconnected"));
    }

    [Fact]
    public async Task NothingStoredIsReportedAsNull()
    {
        var repository = NewInstances();

        (await repository.GetAsync(GatewayId.New())).Should().BeNull();
    }

    private static Domain.ValueObjects.Status.RuntimeStatus RuntimeStatusFrom(string value) =>
        Domain.ValueObjects.Status.RuntimeStatus.From(value);
}
