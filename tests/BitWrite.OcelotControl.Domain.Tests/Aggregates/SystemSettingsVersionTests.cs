using BitWrite.OcelotControl.Domain.Aggregates.SystemSettings;
using BitWrite.OcelotControl.Domain.Exceptions;
using BitWrite.OcelotControl.Domain.Services;
using FluentAssertions;
using Xunit;
using OcelotVersion = BitWrite.OcelotControl.Domain.ValueObjects.Configuration.OcelotVersion;
using SettingsAggregate = BitWrite.OcelotControl.Domain.Aggregates.SystemSettings.SystemSettings;

namespace BitWrite.OcelotControl.Domain.Tests.Aggregates;

/// <summary>
/// The one-time choice of Ocelot version, and the catalog that decides which
/// versions may be offered at all.
/// </summary>
public class SystemSettingsVersionTests
{
    [Fact]
    public void FreshSettingsHaveNoVersion()
    {
        // Not a default of 18: assuming a version is the thing #484 exists to
        // prevent, and it is what the first-run screen exists to ask about.
        var settings = SettingsAggregate.Create();

        settings.OcelotVersion.Should().BeNull();
        settings.IsInitialised.Should().BeFalse();
        settings.OcelotVersionSelectedAt.Should().BeNull();
    }

    [Fact]
    public void ChoosingAVersionInitialisesTheSettings()
    {
        var settings = SettingsAggregate.Create();

        settings.ChooseOcelotVersion(OcelotVersion.V18_0, "admin");

        settings.OcelotVersion.Should().Be(OcelotVersion.V18_0);
        settings.IsInitialised.Should().BeTrue();
        settings.OcelotVersionSelectedAt.Should().NotBeNull();
        settings.OcelotVersionSelectedBy.Should().Be("admin");
    }

    [Fact]
    public void ChoosingRaisesTheEventThatEndsFirstRun()
    {
        var settings = SettingsAggregate.Create();

        settings.ChooseOcelotVersion(OcelotVersion.V18_0, "admin", "corr-1");

        settings.DomainEvents.Should().ContainSingle();
        var raised = settings.DomainEvents.Single().Should().BeOfType<Events.SystemSettingsInitialised>().Subject;
        raised.OcelotVersion.Should().Be(OcelotVersion.V18_0);
        raised.SelectedBy.Should().Be("admin");
        raised.CorrelationId.Should().Be("corr-1");
    }

    [Fact]
    public void TheVersionCannotBeChanged()
    {
        var settings = SettingsAggregate.Create();
        settings.ChooseOcelotVersion(OcelotVersion.V18_0, "admin");

        var ex = Assert.Throws<DomainException>(() => settings.ChooseOcelotVersion(OcelotVersion.V19_0, "someone"));

        ex.ErrorCode.Should().Be("OCELOT_VERSION_ALREADY_CHOSEN");
        // The refusal must not change what is stored.
        settings.OcelotVersion.Should().Be(OcelotVersion.V18_0);
    }

    [Fact]
    public void TheRefusalExplainsWhyRatherThanOnlySayingNo()
    {
        // An operator who cannot change it will ask, and "cannot be changed" on its
        // own reads as an arbitrary rule.
        var settings = SettingsAggregate.Create();
        settings.ChooseOcelotVersion(OcelotVersion.V18_0, "admin");

        var ex = Assert.Throws<DomainException>(() => settings.ChooseOcelotVersion(OcelotVersion.V19_0));

        ex.Message.Should().Contain("already chosen");
        ex.Message.Should().Contain("re-validated");
    }

    [Theory]
    [InlineData("19.0")]
    [InlineData("20.0")]
    [InlineData("23.3")]
    public void AVersionWithNoEstablishedShapesCannotBeChosen(string version)
    {
        // Offering one would promise a shape nobody has written down, and the
        // failure would only surface at a gateway that cannot start.
        var settings = SettingsAggregate.Create();

        var ex = Assert.Throws<DomainException>(
            () => settings.ChooseOcelotVersion(OcelotVersion.Parse(version)));

        ex.ErrorCode.Should().Be("OCELOT_VERSION_NOT_EMITTABLE");
        settings.IsInitialised.Should().BeFalse();
    }

    [Fact]
    public void ChoosingNothingIsRefused()
    {
        var settings = SettingsAggregate.Create();

        var ex = Assert.Throws<DomainException>(() => settings.ChooseOcelotVersion(null!));

        ex.ErrorCode.Should().Be("MISSING_OCELOT_VERSION");
    }

    [Fact]
    public void ReconstituteRestoresTheChosenVersion()
    {
        // Adapters must not use Create: it mints fresh timestamps and drops the
        // version, so a read followed by a save would reset the choice.
        var chosen = DateTimeOffset.UtcNow.AddDays(-3);
        var settings = SettingsAggregate.Reconstitute(
            OcelotVersion.V18_0,
            chosen,
            "admin",
            30,
            90,
            0,
            DateTimeOffset.UtcNow.AddDays(-10),
            chosen);

        settings.OcelotVersion.Should().Be(OcelotVersion.V18_0);
        settings.OcelotVersionSelectedAt.Should().Be(chosen);
        settings.OcelotVersionSelectedBy.Should().Be("admin");
        settings.IsInitialised.Should().BeTrue();
    }

    [Fact]
    public void ReconstituteCanRestoreAnUninitialisedState()
    {
        var settings = SettingsAggregate.Reconstitute(
            null, null, null, 30, 90, 0, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        settings.IsInitialised.Should().BeFalse();
    }

    [Theory]
    [InlineData(4)]
    [InlineData(0)]
    [InlineData(-1)]
    public void AnAbsurdlyShortPollIntervalIsRefused(int seconds)
    {
        // Below this a gateway spends its time asking rather than serving.
        var settings = SettingsAggregate.Create();

        var ex = Assert.Throws<DomainException>(() => settings.SetPollInterval(seconds));

        ex.ErrorCode.Should().Be("INVALID_POLL_INTERVAL");
    }

    [Fact]
    public void ARetentionOfZeroMeansKeepForever()
    {
        // A valid value, not a missing one, so it must not be rejected as zero.
        var settings = SettingsAggregate.Create();

        settings.SetAuditLogRetention(0);
        settings.SetSnapshotRetention(0);

        settings.AuditLogRetentionDays.Should().Be(0);
        settings.SnapshotRetentionCount.Should().Be(0);
    }

    [Fact]
    public void NegativeRetentionIsRefused()
    {
        var settings = SettingsAggregate.Create();

        settings.Invoking(s => s.SetAuditLogRetention(-1))
            .Should().Throw<DomainException>();
        settings.Invoking(s => s.SetSnapshotRetention(-1))
            .Should().Throw<DomainException>();
    }

    [Fact]
    public void OperationalSettingsDoNotDisturbTheVersion()
    {
        var settings = SettingsAggregate.Create();
        settings.ChooseOcelotVersion(OcelotVersion.V18_0, "admin");

        settings.SetPollInterval(45);
        settings.SetAuditLogRetention(30);
        settings.SetSnapshotRetention(10);

        settings.OcelotVersion.Should().Be(OcelotVersion.V18_0);
        settings.PollIntervalSeconds.Should().Be(45);
        settings.AuditLogRetentionDays.Should().Be(30);
        settings.SnapshotRetentionCount.Should().Be(10);
    }
}

/// <summary>
/// Which versions may be offered, and what happens when the builder is asked for
/// one that may not be.
/// </summary>
public class OcelotVersionCatalogTests
{
    [Fact]
    public void EighteenIsTheOnlyVersionOffered()
    {
        // 19 and 20 exist in the version type and are named in the issue, but
        // their shapes are not established, so the list has one entry. That is
        // the honest state, not an oversight.
        OcelotVersionCatalog.EmittableVersions()
            .Select(version => version.ToString())
            .Should().Equal("18.0.0");
    }

    [Fact]
    public void TheOfferedListIsNewestFirst()
    {
        var versions = OcelotVersionCatalog.EmittableVersions();

        versions.Should().BeInDescendingOrder();
    }

    [Theory]
    [InlineData("18.0.0", true)]
    [InlineData("19.0.0", false)]
    [InlineData("20.0.0", false)]
    [InlineData("23.3.0", false)]
    public void EmittabilityIsExactRatherThanAThreshold(string version, bool expected)
    {
        // A threshold would offer 20 the moment 18 exists, which is the bug this
        // replaces. Exact match is what "a shape exists for this version" means.
        OcelotVersionCatalog.IsEmittable(OcelotVersion.Parse(version)).Should().Be(expected);
    }

    [Fact]
    public void TheBuilderRefusesToGuessAVersion()
    {
        // Generating a configuration with no version chosen would publish a file
        // shaped for a version nobody selected.
        var ex = Assert.Throws<DomainException>(() => OcelotVersionCatalog.RequireConfigured(null));

        ex.ErrorCode.Should().Be("OCELOT_VERSION_NOT_CONFIGURED");
    }

    [Fact]
    public void TheBuilderRefusesAVersionWithNoShapes()
    {
        var ex = Assert.Throws<NotExpressibleException>(
            () => OcelotVersionCatalog.RequireConfigured(OcelotVersion.V20_0));

        ex.Field.Should().Be("ocelotVersion");
    }

    [Fact]
    public void AConfiguredVersionIsReturnedAsIs()
    {
        OcelotVersionCatalog.RequireConfigured(OcelotVersion.V18_0).Should().Be(OcelotVersion.V18_0);
    }
}
