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
/// Gateway records in Redis.
///
/// Two documents, two keys, two writers. The instance record belongs to the control
/// plane and carries the status; the heartbeat belongs to the runtime and says when
/// it last reported in and what it is running.
///
/// They used to share one key. The runtime's heartbeat carried no status, so every
/// 30 seconds it replaced a record that had one — and a gateway that had just applied
/// a configuration was reported Disconnected, because reading it back found nothing
/// but the fields the heartbeat happened to include.
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
        return new RedisRuntimeInstanceRepository(mux.Object, TestEnvironment.Context());
    }

    /// <summary>
    /// The shape the runtime itself writes, in RuntimeAdapter.UpdateHeartbeatAsync.
    /// </summary>
    private static string Heartbeat(string gatewayId, string? version, string timestamp) =>
        $$"""{"GatewayId":"{{gatewayId}}","Version":{{(version is null ? "null" : $"\"{version}\"")}},"Timestamp":"{{timestamp}}"}""";

    /// <summary>The shape the control plane writes, in AddAsync. Carries a status.</summary>
    private static string Instance(string gatewayId, string? version, string status, string timestamp) =>
        $$"""{"GatewayId":"{{gatewayId}}","Version":{{(version is null ? "null" : $"\"{version}\"")}},"Status":"{{status}}","Timestamp":"{{timestamp}}"}""";

    private static string InstanceKey(GatewayId id) => $"ocelot:runtime:gateway:{id.Value}";

    private static string HeartbeatKey(GatewayId id) => $"{InstanceKey(id)}:heartbeat";

    [Fact]
    public async Task AHeartbeatWrittenAsJsonIsRead()
    {
        // The bug: this was read as a hash, found nothing, and reported every
        // gateway as having no runtime data at all.
        var repository = NewInstances();
        var id = GatewayId.New();
        var beat = DateTimeOffset.UtcNow.AddSeconds(-30);
        _strings[InstanceKey(id)] =
            Instance(id.Value.ToString(), "7", "Active", beat.ToString("O"));
        _strings[HeartbeatKey(id)] =
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
        _strings[InstanceKey(id)] =
            Instance(id.Value.ToString(), "7", "Active", DateTimeOffset.UtcNow.ToString("O"));
        _strings[HeartbeatKey(id)] =
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
        _strings[InstanceKey(id)] =
            Instance(id.Value.ToString(), null, "Connecting", DateTimeOffset.UtcNow.ToString("O"));
        _strings[HeartbeatKey(id)] =
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
        _strings[InstanceKey(id)] =
            Instance(id.Value.ToString(), "3", "Active", old.ToString("O"));
        _strings[HeartbeatKey(id)] =
            Heartbeat(id.Value.ToString(), "3", old.ToString("O"));

        var loaded = await repository.GetAsync(id);

        loaded!.LastHeartbeat.Should().BeCloseTo(old, TimeSpan.FromSeconds(1));
        loaded.Status.Should().NotBe(RuntimeStatusFrom("Connecting"));
    }

    [Fact]
    public async Task AStoredStatusSurvivesLaterHeartbeats()
    {
        // The bug: the heartbeat carried no status and shared the instance key, so
        // within 30 seconds a gateway that had applied a configuration was reported
        // Disconnected — not because it was, but because the record saying so had
        // been replaced by one that had no opinion.
        var repository = NewInstances();
        var id = GatewayId.New();
        var applied = DateTimeOffset.UtcNow.AddMinutes(-5);
        _strings[InstanceKey(id)] =
            Instance(id.Value.ToString(), "2", "Active", applied.ToString("O"));

        // Several beats arrive, each reporting the gateway is alive and running v2.
        for (var beat = 1; beat <= 3; beat++)
        {
            _strings[HeartbeatKey(id)] =
                Heartbeat(id.Value.ToString(), "2", DateTimeOffset.UtcNow.ToString("O"));
            (await repository.GetAsync(id))!.Status.Should().Be(RuntimeStatusFrom("Active"));
        }
    }

    [Fact]
    public async Task AHeartbeatUpdatesWhenTheGatewayLastReportedIn()
    {
        // Liveness is the heartbeat's to answer, and it keeps answering it: the
        // instance record is written when configuration changes, which is rare.
        var repository = NewInstances();
        var id = GatewayId.New();
        var applied = DateTimeOffset.UtcNow.AddHours(-2);
        _strings[InstanceKey(id)] =
            Instance(id.Value.ToString(), "2", "Active", applied.ToString("O"));

        var beat = DateTimeOffset.UtcNow.AddSeconds(-20);
        _strings[HeartbeatKey(id)] = Heartbeat(id.Value.ToString(), "2", beat.ToString("O"));

        var loaded = await repository.GetAsync(id);

        loaded!.LastHeartbeat.Should().BeCloseTo(beat, TimeSpan.FromSeconds(1));
        loaded.Status.Should().Be(RuntimeStatusFrom("Active"));
    }

    [Fact]
    public async Task AGatewayThatHasAppliedNothingYetIsNotReportedAsDisconnected()
    {
        // Nothing has been applied, so there is no Active to report — but the
        // gateway is registered and answering, which is not the same as gone.
        var repository = NewInstances();
        var id = GatewayId.New();
        _strings[InstanceKey(id)] =
            Instance(id.Value.ToString(), null, "Connecting", DateTimeOffset.UtcNow.ToString("O"));
        _strings[HeartbeatKey(id)] =
            Heartbeat(id.Value.ToString(), null, DateTimeOffset.UtcNow.ToString("O"));

        var loaded = await repository.GetAsync(id);

        loaded!.Status.Should().Be(RuntimeStatusFrom("Connecting"));
    }

    [Fact]
    public async Task ARecordWrittenBeforeHeartbeatsHadTheirOwnKeyIsStillRead()
    {
        // Deployments upgrading have the runtime's document on the instance key and
        // no heartbeat key at all. That is read as it always was — no status in it,
        // so no status claimed — rather than becoming unreadable.
        var repository = NewInstances();
        var id = GatewayId.New();
        var beat = DateTimeOffset.UtcNow.AddHours(-1);
        _strings[InstanceKey(id)] = Heartbeat(id.Value.ToString(), "3", beat.ToString("O"));

        var loaded = await repository.GetAsync(id);

        loaded.Should().NotBeNull();
        loaded!.LastHeartbeat.Should().BeCloseTo(beat, TimeSpan.FromSeconds(1));
        loaded.Status.Should().Be(RuntimeStatusFrom("Disconnected"));
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
