using BitWrite.OcelotControl.Domain.Aggregates.SystemSettings;
using BitWrite.OcelotControl.Domain.Exceptions;
using BitWrite.OcelotControl.Domain.Services;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using FluentAssertions;
using Xunit;

namespace BitWrite.OcelotControl.Domain.Tests.Aggregates;

/// <summary>
/// The Ocelot version choice, and the fact that it is permanent.
/// </summary>
/// <remarks>
/// The API endpoint that makes this choice is open to an unauthenticated caller,
/// because nothing can issue a token yet and a guarded one left a fresh install
/// unable to configure itself (#503). This is the guard that took its place: it is
/// in the domain, it does not consult a role, and it applies to every caller alike.
/// If this ever loosened, an open endpoint would be a permanent-choice endpoint that
/// answers yes.
/// </remarks>
public class SystemSettingsOcelotVersionTests
{
    [Fact]
    public void AnUninitialisedInstallHasNoVersion()
    {
        var settings = SystemSettings.Create();

        settings.OcelotVersion.Should().BeNull();
        settings.IsInitialised.Should().BeFalse();
    }

    [Fact]
    public void ChoosingAVersionInitialisesTheInstall()
    {
        var settings = SystemSettings.Create();

        settings.ChooseOcelotVersion(OcelotVersion.V18_0, "operator");

        settings.OcelotVersion.Should().Be(OcelotVersion.V18_0);
        settings.IsInitialised.Should().BeTrue();
    }

    [Fact]
    public void RecordsWhoChoseItAndWhen()
    {
        // The choice decides the shape of every configuration this installation
        // will generate, and cannot be undone, so the audit trail has to be able
        // to say who made it.
        var settings = SystemSettings.Create();

        settings.ChooseOcelotVersion(OcelotVersion.V18_0, "alex");

        settings.OcelotVersionSelectedBy.Should().Be("alex");
        settings.OcelotVersionSelectedAt.Should().NotBeNull();
    }

    [Fact]
    public void RefusesADifferentVersionOnceOneIsChosen()
    {
        // The important one. The endpoint is open, so this is what stands between
        // an unauthenticated caller and a control plane that changes the shape of
        // every configuration it has already published.
        var settings = SystemSettings.Create();
        settings.ChooseOcelotVersion(OcelotVersion.V18_0, "operator");

        var choose = () => settings.ChooseOcelotVersion(OcelotVersion.V19_0, "someone-else");

        choose.Should().Throw<DomainException>();
        settings.OcelotVersion.Should().Be(OcelotVersion.V18_0, "the choice did not move");
    }

    [Fact]
    public void RefusesEvenTheSameVersionOnASecondCall()
    {
        // The controller's remark claimed this was idempotent; the domain refuses
        // it. A second caller is refused whatever it sends, which is the stricter
        // reading and the safer one for a permanent choice — so the comment was
        // what needed correcting, not the behaviour.
        var settings = SystemSettings.Create();
        settings.ChooseOcelotVersion(OcelotVersion.V18_0, "operator");

        var choose = () => settings.ChooseOcelotVersion(OcelotVersion.V18_0, "other-operator");

        choose.Should().Throw<DomainException>()
            .WithMessage("*already chosen*");
        settings.OcelotVersion.Should().Be(OcelotVersion.V18_0);
    }

    [Fact]
    public void RefusesAVersionTheProductCannotEmit()
    {
        // Choosing 20.0 would promise configurations the builder refuses to
        // generate, so setup would complete and then every snapshot would fail.
        var settings = SystemSettings.Create();

        var choose = () => settings.ChooseOcelotVersion(OcelotVersion.V20_0, "operator");

        // A plain domain failure, not the shape-specific one: the version is
        // refused before anything tries to express it.
        choose.Should().Throw<DomainException>()
            .WithMessage("*cannot be targeted yet*");
        settings.OcelotVersion.Should().BeNull("a refused choice is not half a choice");
        settings.IsInitialised.Should().BeFalse();
    }

    [Fact]
    public void ListsOnlyTheVersionsItCanActuallyEmit()
    {
        // The setup screen offers exactly this, so offering a version the builder
        // rejects would be offering a choice that cannot be completed.
        OcelotVersionCatalog.EmittableVersions()
            .Should().BeEquivalentTo(new[] { OcelotVersion.V18_0 });
    }
}
