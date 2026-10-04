using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Infrastructure.Repositories;
using FluentAssertions;
using Moq;
using StackExchange.Redis;
using Xunit;
using OcelotVersion = BitWrite.OcelotControl.Domain.ValueObjects.Configuration.OcelotVersion;
using SettingsAggregate = BitWrite.OcelotControl.Domain.Aggregates.SystemSettings.SystemSettings;

namespace BitWrite.OcelotControl.Infrastructure.Tests.Repositories;

/// <summary>
/// The chosen Ocelot version has to survive a round trip, because losing it
/// would put a configured installation back into first-run — or, worse, let the
/// builder guess.
/// </summary>
public class SystemSettingsPersistenceTests
{
    private readonly Mock<IDatabase> _db = new();
    private readonly Dictionary<string, RedisValue> _store = new(StringComparer.Ordinal);

    public SystemSettingsPersistenceTests()
    {
        _db.Setup(d => d.StringSetAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .Callback<RedisKey, RedisValue, TimeSpan?, bool, When, CommandFlags>(
                (key, value, _, _, _, _) => _store[key.ToString()!] = value)
            .ReturnsAsync(true);

        // The factory form matters: a plain Returns value is captured when the
        // setup is made, so the write above would never be visible to a later
        // read and every round trip would come back empty.
        _db.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(() =>
                _store.TryGetValue("ocelot:settings", out var value) ? value : RedisValue.Null);
    }

    private ISystemSettingsRepository NewRepository()
    {
        var mux = new Mock<IConnectionMultiplexer>();
        mux.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_db.Object);
        return new RedisSystemSettingsRepository(mux.Object, TestEnvironment.Context());
    }

    private async Task<SettingsAggregate> RoundTrip(SettingsAggregate settings)
    {
        var repository = NewRepository();
        await repository.UpdateAsync(settings);
        return await repository.GetAsync();
    }

    [Fact]
    public async Task TheChosenVersionSurvivesARoundTrip()
    {
        var settings = SettingsAggregate.Create();
        settings.ChooseOcelotVersion(OcelotVersion.V18_0, "admin");

        var loaded = await RoundTrip(settings);

        loaded.OcelotVersion.Should().Be(OcelotVersion.V18_0);
        loaded.IsInitialised.Should().BeTrue();
        loaded.OcelotVersionSelectedBy.Should().Be("admin");
        loaded.OcelotVersionSelectedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task OperationalSettingsSurviveARoundTrip()
    {
        var settings = SettingsAggregate.Create();
        settings.ChooseOcelotVersion(OcelotVersion.V18_0, "admin");
        settings.SetPollInterval(45);
        settings.SetAuditLogRetention(30);
        settings.SetSnapshotRetention(7);

        var loaded = await RoundTrip(settings);

        loaded.PollIntervalSeconds.Should().Be(45);
        loaded.AuditLogRetentionDays.Should().Be(30);
        loaded.SnapshotRetentionCount.Should().Be(7);
    }

    [Fact]
    public async Task ReadingWithNothingStoredReportsSetupAsOutstanding()
    {
        // This is the state a first-run screen depends on, and it has to be
        // distinguishable from "stored, but uninitialised".
        var repository = NewRepository();

        var loaded = await repository.GetAsync();

        loaded.IsInitialised.Should().BeFalse();
        loaded.OcelotVersion.Should().BeNull();
    }

    [Fact]
    public async Task AnUninitialisedStoredRowStillReadsAsUninitialised()
    {
        var repository = NewRepository();
        await repository.UpdateAsync(SettingsAggregate.Create());

        var loaded = await repository.GetAsync();

        loaded.IsInitialised.Should().BeFalse();
    }

    [Fact]
    public async Task AStoredVersionThisBuildCannotParseIsTreatedAsNotChosen()
    {
        // Rather than failing every read of the settings, a value this build
        // cannot read puts the installation back into first-run. Choosing again
        // is still refused if the raw value survives, so this is a degraded state
        // rather than a silent reset.
        var repository = NewRepository();
        await repository.UpdateAsync(SettingsAggregate.Create());
        _store["ocelot:settings"] = """{"OcelotVersion":"not-a-version"}""";

        var loaded = await repository.GetAsync();

        loaded.OcelotVersion.Should().BeNull();
        loaded.IsInitialised.Should().BeFalse();
    }

    [Fact]
    public async Task TheVersionIsStoredUnderItsOwnKey()
    {
        var repository = NewRepository();
        var settings = SettingsAggregate.Create();
        settings.ChooseOcelotVersion(OcelotVersion.V18_0, "admin");

        await repository.UpdateAsync(settings);

        _store.Should().ContainKey("ocelot:settings");
        _store["ocelot:settings"].ToString().Should().Contain("18.0.0");
    }
}
