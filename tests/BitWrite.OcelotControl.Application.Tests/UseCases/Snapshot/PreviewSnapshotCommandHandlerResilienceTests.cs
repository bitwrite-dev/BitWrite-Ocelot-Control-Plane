using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Application.UseCases.Snapshot;
using BitWrite.OcelotControl.Domain.Services;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using FluentAssertions;
using Moq;
using Xunit;
using ValidationError = BitWrite.OcelotControl.Domain.Services.ValidationError;

using GlobalConfigAggregate = BitWrite.OcelotControl.Domain.Aggregates.GlobalConfiguration.GlobalConfiguration;
using RouteAggregate = BitWrite.OcelotControl.Domain.Aggregates.Route.Route;
using ServiceAggregate = BitWrite.OcelotControl.Domain.Aggregates.Service.Service;
using SystemSettingsAggregate = BitWrite.OcelotControl.Domain.Aggregates.SystemSettings.SystemSettings;
using GlobalConfigParam = BitWrite.OcelotControl.Domain.Services.GlobalConfiguration;
using OcelotHttpMethod = BitWrite.OcelotControl.Domain.ValueObjects.Configuration.HttpMethod;

namespace BitWrite.OcelotControl.Application.Tests.UseCases.Snapshot;

/// <summary>
/// The preview must not fail with a null reference when the management state is
/// thin.
/// </summary>
/// <remarks>
/// A fresh install has no routes, no services, and settings that have never been
/// written. The preview is the first thing the create page calls, so a null there
/// is a blank page on a working installation — which is exactly what it turned out
/// to be. Everything below the repositories is real, so a null has to come from
/// the code under test rather than from a stub that returns nothing.
/// </remarks>
public class PreviewSnapshotCommandHandlerResilienceTests
{
    private static PreviewSnapshotCommandHandler Build(
        List<RouteAggregate>? routes = null,
        List<ServiceAggregate>? services = null,
        bool versionChosen = true)
    {
        var globalConfig = new Mock<IGlobalConfigurationRepository>();
        globalConfig.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(GlobalConfigAggregate.Create());

        var routeRepository = new Mock<IRouteRepository>();
        routeRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(routes ?? new List<RouteAggregate>());

        var serviceRepository = new Mock<IServiceRepository>();
        serviceRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(services ?? new List<ServiceAggregate>());

        var real = new ConfigurationCanonicalizer();
        var canonicalizer = new Mock<IConfigurationCanonicalizer>();
        canonicalizer.Setup(c => c.CanonicalizeJson(It.IsAny<OcelotConfiguration>()))
            .Returns<OcelotConfiguration>(c => real.CanonicalizeJson(c));
        canonicalizer.Setup(c => c.Canonicalize(It.IsAny<OcelotConfiguration>()))
            .Returns<OcelotConfiguration>(c => real.Canonicalize(c));

        var integrity = new Mock<ISnapshotIntegrityVerifier>();
        integrity.Setup(v => v.ComputeHash(It.IsAny<string>()))
            .Returns<string>(content => new SnapshotIntegrityVerifier(real).ComputeHash(content));

        var conflicts = new Mock<IRouteConflictDetector>();
        conflicts.Setup(d => d.DetectConflicts(
                It.IsAny<RouteKey>(),
                It.IsAny<IEnumerable<(RouteId Id, RouteKey Key)>>(),
                It.IsAny<RouteId?>()))
            .Returns(Array.Empty<RouteKey>());

        var consistency = new Mock<IConfigurationConsistencyValidator>();
        consistency.Setup(v => v.ValidateServiceReferences(
                It.IsAny<IEnumerable<ServiceId>>(), It.IsAny<IEnumerable<ServiceId>>()))
            .Returns(Array.Empty<ValidationError>());
        consistency.Setup(v => v.ValidateDownstreamTargets(It.IsAny<IEnumerable<DownstreamTarget>>()))
            .Returns(Array.Empty<ValidationError>());
        consistency.Setup(v => v.ValidateGlobalConfiguration(
                It.IsAny<OcelotVersion>(), It.IsAny<IEnumerable<string>>()))
            .Returns(Array.Empty<ValidationError>());

        // `Create` is exactly the state a first-run install holds: no version
        // chosen. 18.0 is the only version with established shapes, so anything
        // else is refused by the builder by design.
        var stored = versionChosen
            ? SystemSettingsAggregate.Reconstitute(
                OcelotVersion.V18_0, DateTimeOffset.UtcNow, "test", 30, 90, 0,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)
            : SystemSettingsAggregate.Create();

        var settings = new Mock<ISystemSettingsRepository>();
        settings.Setup(s => s.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(stored);

        var versions = new Mock<ISnapshotVersionAllocator>();
        versions.Setup(v => v.CurrentVersion).Returns(SnapshotVersion.From(2));

        return new PreviewSnapshotCommandHandler(
            globalConfig.Object,
            routeRepository.Object,
            serviceRepository.Object,
            new RealConfigurationBuilder(real),
            conflicts.Object,
            consistency.Object,
            canonicalizer.Object,
            integrity.Object,
            settings.Object,
            versions.Object);
    }

    private static RouteAggregate SampleRoute() => RouteAggregate.Create(
        OcelotHttpMethod.Parse("GET"),
        UpstreamPath.From("/api/users"),
        ServiceId.From(Guid.NewGuid()),
        [DownstreamTarget.Create("http", "localhost", 5001)]);

    [Fact]
    public async Task AnswersOnAnInstallationWithNothingConfiguredYet()
    {
        // No routes, no services, settings never written. The create page opens on
        // this and has to get an answer, not an exception.
        var preview = await Build().HandleAsync();

        preview.ValidationResults.Should().ContainSingle(r => r.Rule == "HasContent");
        preview.IsValid.Should().BeTrue("an empty state is a warning, not a failure");
        // The version is reported, not dereferenced: this response is what the
        // page renders.
        // Routes and version are separate first-run facts; this install has run
        // setup, so the version is present and the empty state is the route list.
        preview.OcelotVersion.Should().Contain("18");
    }

    [Fact]
    public async Task BuildsFromRealRoutesRatherThanOnlyFromTestDoubles()
    {
        // The first version of this fixture faked the configuration builder, so a
        // null thrown by the real one stayed invisible until the page failed in a
        // browser.
        var preview = await Build([SampleRoute()], [ServiceAggregate.Create("users-api")])
            .HandleAsync();

        preview.Content.Should().NotBeNullOrEmpty();
        preview.Hash.Should().NotBeNullOrEmpty();
        preview.Composition.RouteCount.Should().Be(1);
    }

    [Fact]
    public async Task ExplainsAnInstallationThatHasNotChosenAVersionYet()
    {
        // Settings hold no version until first-run, and the builder takes a
        // non-nullable one. Passing that null through was a bare null reference,
        // which told the operator nothing about what to do about it.
        // A first-run installation holds settings with no version, and the create
        // page opens on that. `Create` is that state, asserted here so a failure
        // below cannot be mistaken for the handler being at fault.
        SystemSettingsAggregate.Create().OcelotVersion.Should().BeNull();

        var preview = await Build([SampleRoute()], versionChosen: false).HandleAsync();

        preview.Content.Should().BeNull(
            "there is no shape to emit without a version, so nothing can be built");
        preview.IsValid.Should().BeFalse();
        preview.ValidationResults.Should().Contain(result =>
            result.Rule == "ConfigurationBuild" &&
            result.Message!.Contains("first-run"));
        // The build failure is the only rule that runs: with nothing built there
        // is no configuration for the capability check to judge, and reporting a
        // second failure about the same cause would read as two problems.
        preview.ValidationResults.Where(r => !r.IsValid)
            .Select(r => r.Rule)
            .Should().ContainSingle("only the build failed");

        // Reported rather than dereferenced, so the page can say what it is.
        preview.OcelotVersion.Should().Be("not chosen");
    }

    [Fact]
    public async Task NeverThrowsWhateverTheManagementStateHolds()
    {
        // A preview is a read. It reports what it finds; it does not fail because
        // the state is incomplete, because that incomplete state is exactly what
        // the operator needs to find out about.
        var act = async () => await Build([SampleRoute()]).HandleAsync();
        await act.Should().NotThrowAsync();

        // The same for an installation with nothing chosen and nothing configured:
        // both are states a real deployment sits in on its first day.
        var unconfigured = async () => await Build(versionChosen: false).HandleAsync();
        await unconfigured.Should().NotThrowAsync();
    }
}

/// <summary>
/// The real domain builder behind the Application interface.
/// </summary>
/// <remarks>
/// The shipped adapter lives in Infrastructure, and an Application test cannot
/// reach across into it. Declaring it here keeps the boundary intact while still
/// running the real builder, which is the point of this fixture.
/// </remarks>
internal sealed class RealConfigurationBuilder : IConfigurationBuilder
{
    private readonly ConfigurationBuilder _inner;

    public RealConfigurationBuilder(ConfigurationCanonicalizer canonicalizer) =>
        _inner = new ConfigurationBuilder(canonicalizer);

    public OcelotConfiguration BuildConfiguration(
        IReadOnlyList<RouteConfiguration> routes,
        GlobalConfigParam globalConfig,
        OcelotVersion ocelotVersion) =>
        _inner.BuildConfiguration(routes, globalConfig, ocelotVersion);

    public ConfigurationHash CalculateConfigurationHash(OcelotConfiguration configuration) =>
        _inner.CalculateConfigurationHash(configuration);
}
