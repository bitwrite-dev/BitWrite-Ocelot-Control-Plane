using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Application.UseCases.Snapshot;
using BitWrite.OcelotControl.Domain.Services;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using FluentAssertions;
using Moq;
using Xunit;
using GlobalConfigAggregate = BitWrite.OcelotControl.Domain.Aggregates.GlobalConfiguration.GlobalConfiguration;
using GlobalConfigParam = BitWrite.OcelotControl.Domain.Services.GlobalConfiguration;
// System.Net.Http also defines HttpMethod, and it is implicitly in scope.
using OcelotHttpMethod = BitWrite.OcelotControl.Domain.ValueObjects.Configuration.HttpMethod;
using RouteAggregate = BitWrite.OcelotControl.Domain.Aggregates.Route.Route;
using ServiceAggregate = BitWrite.OcelotControl.Domain.Aggregates.Service.Service;
using SystemSettingsAggregate = BitWrite.OcelotControl.Domain.Aggregates.SystemSettings.SystemSettings;
using ValidationError = BitWrite.OcelotControl.Domain.Services.ValidationError;

namespace BitWrite.OcelotControl.Application.Tests.UseCases.Snapshot;

/// <summary>
/// The preview that makes a create page able to show the state before sealing it.
/// </summary>
/// <remarks>
/// Creating used to build, validate, hash, version and store in one call, which
/// left the operator no point at which to look at what was about to become
/// immutable. The preview does everything except the last two of those.
/// <para>
/// The behaviour that matters most here is what it refuses to do: a preview must
/// not spend a version, because those numbers are what a rollback names.
/// </para>
/// </remarks>
public class PreviewSnapshotCommandHandlerTests
{
    private static OcelotConfiguration SampleConfiguration() => new()
    {
        GlobalConfiguration = new OcelotGlobalConfiguration
        {
            BaseUrl = "http://localhost:5000",
            RequestIdKey = "X-Request-ID",
        },
        Routes =
        [
            new()
            {
                UpstreamPathTemplate = "/api/orders",
                UpstreamHttpMethod = ["GET"],
                DownstreamHostAndPorts = [new() { Host = "localhost", Port = 5001 }],
            },
        ],
    };

    /// <summary>
    /// One real route, so the conflict detector is actually reached.
    /// </summary>
    /// <remarks>
    /// Detection runs per route, so an empty route list means the loop never
    /// executes and a stubbed conflict is never observed — the test would pass for
    /// the wrong reason.
    /// </remarks>
    private static RouteAggregate SampleRoute() => RouteAggregate.Create(
        OcelotHttpMethod.Parse("GET"),
        UpstreamPath.From("/api/orders"),
        ServiceId.From(Guid.NewGuid()),
        [DownstreamTarget.Create("http", "localhost", 5001)]);

    private sealed class Harness
    {
        public Mock<IConfigurationBuilder> Builder { get; } = new();
        public Mock<IRouteConflictDetector> Conflicts { get; } = new();
        public Mock<IConfigurationConsistencyValidator> Consistency { get; } = new();
        public Mock<ISnapshotVersionAllocator> Versions { get; } = new();
        public Mock<IRouteRepository> Routes { get; } = new();
        public List<RouteAggregate> RouteList { get; } = [];
        public Mock<IServiceRepository> Services { get; } = new();
        public Mock<IGlobalConfigurationRepository> GlobalConfig { get; } = new();
    }

    /// <summary>
    /// Builds the handler, letting the caller adjust the configuration builder
    /// first.
    /// </summary>
    /// <remarks>
    /// The builder's behaviour has to be arranged before the handler exists,
    /// because <c>BuildConfiguration</c> is called during the request, not during
    /// construction — but a late <c>Setup</c> on the same mock wins, so arranging
    /// it here is what makes a throwing builder actually throw.
    /// </remarks>
    private static PreviewSnapshotCommandHandler BuildHandler(
        Harness harness,
        OcelotConfiguration? configuration = null,
        Action<Harness>? arrange = null)
    {
        var canonicalizer = new Mock<IConfigurationCanonicalizer>();
        canonicalizer.Setup(c => c.CanonicalizeJson(It.IsAny<OcelotConfiguration>()))
            .Returns<OcelotConfiguration>(c => new ConfigurationCanonicalizer().CanonicalizeJson(c));

        var integrityVerifier = new Mock<ISnapshotIntegrityVerifier>();
        integrityVerifier.Setup(v => v.ComputeHash(It.IsAny<string>()))
            .Returns<string>(content =>
                new SnapshotIntegrityVerifier(new ConfigurationCanonicalizer()).ComputeHash(content));

        harness.Builder.Setup(b => b.BuildConfiguration(
                It.IsAny<IReadOnlyList<RouteConfiguration>>(),
                It.IsAny<GlobalConfigParam>(),
                It.IsAny<OcelotVersion>()))
            .Returns(configuration ?? SampleConfiguration());

        harness.Conflicts.Setup(d => d.DetectConflicts(
                It.IsAny<RouteKey>(),
                It.IsAny<IEnumerable<(RouteId Id, RouteKey Key)>>(),
                It.IsAny<RouteId?>()))
            .Returns(Array.Empty<RouteKey>());

        harness.Consistency.Setup(v => v.ValidateServiceReferences(
                It.IsAny<IEnumerable<ServiceId>>(),
                It.IsAny<IEnumerable<ServiceId>>()))
            .Returns(Array.Empty<ValidationError>());
        harness.Consistency.Setup(v => v.ValidateDownstreamTargets(It.IsAny<IEnumerable<DownstreamTarget>>()))
            .Returns(Array.Empty<ValidationError>());
        harness.Consistency.Setup(v => v.ValidateGlobalConfiguration(
                It.IsAny<OcelotVersion>(), It.IsAny<IEnumerable<string>>()))
            .Returns(Array.Empty<ValidationError>());

        harness.GlobalConfig.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(GlobalConfigAggregate.Create());
        harness.Routes.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => harness.RouteList);
        harness.Services.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ServiceAggregate>());

        var settings = new Mock<ISystemSettingsRepository>();
        settings.Setup(s => s.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(SystemSettingsAggregate.Reconstitute(
                OcelotVersion.V20_0,
                DateTimeOffset.UtcNow,
                "test",
                30,
                90,
                0,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow));

        harness.Versions.Setup(v => v.CurrentVersion).Returns(SnapshotVersion.From(104));

        // Last, so a caller's arrangement is what the handler actually sees:
        // an earlier Setup on the same mock is overwritten by a later one.
        arrange?.Invoke(harness);

        return new PreviewSnapshotCommandHandler(
            harness.GlobalConfig.Object,
            harness.Routes.Object,
            harness.Services.Object,
            harness.Builder.Object,
            harness.Conflicts.Object,
            harness.Consistency.Object,
            canonicalizer.Object,
            integrityVerifier.Object,
            settings.Object,
            harness.Versions.Object);
    }

    [Fact]
    public async Task ResolvesTheArtifactWithoutStoringIt()
    {
        var harness = new Harness();

        var preview = await BuildHandler(harness).HandleAsync();

        preview.Content.Should().NotBeNullOrEmpty();
        preview.Hash.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ReportsTheVersionItWouldTakeWithoutSpendingIt()
    {
        var harness = new Harness();

        var preview = await BuildHandler(harness).HandleAsync();

        preview.NextVersion.Should().Be(105);
        // The numbers are what a rollback names. Allocating here would burn one
        // on a preview that gets abandoned.
        harness.Versions.Verify(v => v.AllocateNext(), Times.Never);
        harness.Versions.Verify(v => v.AllocateSpecific(It.IsAny<SnapshotVersion>()), Times.Never);
    }

    [Fact]
    public async Task HashesTheExactStringItWouldStore()
    {
        var harness = new Harness();

        var preview = await BuildHandler(harness).HandleAsync();

        // If the hash covered the object instead of the canonical string, the
        // previewed hash would not match the one the snapshot ends up carrying.
        var expected = new SnapshotIntegrityVerifier(new ConfigurationCanonicalizer())
            .ComputeHash(preview.Content!);

        preview.Hash.Should().Be(expected);
    }

    [Fact]
    public async Task ReportsTheCompositionOfTheArtifactItBuilt()
    {
        var harness = new Harness();

        var preview = await BuildHandler(harness).HandleAsync();

        preview.Composition.RouteCount.Should().Be(1);
        // The built document declares Routes and GlobalConfiguration only, so it
        // carries no Services array to count. Reporting 0 is the honest answer
        // for this shape, and a real deployment document will differ.
        preview.Composition.ServiceCount.Should().Be(0);
    }

    [Fact]
    public async Task ReportsNoContentRatherThanAnEmptyArtifactWhenTheStateCannotBeBuilt()
    {
        var harness = new Harness();
        var handler = BuildHandler(harness, arrange: h =>
            h.Builder.Setup(b => b.BuildConfiguration(
                    It.IsAny<IReadOnlyList<RouteConfiguration>>(),
                    It.IsAny<GlobalConfigParam>(),
                    It.IsAny<OcelotVersion>()))
                .Throws(new InvalidOperationException("a downstream target is not resolvable")));

        var preview = await handler.HandleAsync();

        // An empty document would read as a valid artifact with nothing in it.
        preview.Content.Should().BeNull();
        preview.Hash.Should().BeNull();
        var build = preview.ValidationResults.Single(r => r.Rule == "ConfigurationBuild");
        build.IsValid.Should().BeFalse();
        build.Message.Should().Contain("not resolvable");
        preview.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task ReportsAConflictAsAFailedRuleRatherThanThrowing()
    {
        var harness = new Harness();
        harness.RouteList.Add(SampleRoute());
        var handler = BuildHandler(harness, arrange: h =>
            h.Conflicts.Setup(d => d.DetectConflicts(
                    It.IsAny<RouteKey>(),
                    It.IsAny<IEnumerable<(RouteId Id, RouteKey Key)>>(),
                    It.IsAny<RouteId?>()))
                .Returns(new[]
                {
                    RouteKey.Create(
                        OcelotHttpMethod.Parse("GET"),
                        UpstreamPath.From("/api/orders"),
                        "localhost"),
                }));

        var preview = await handler.HandleAsync();

        // Throwing would make "your routes conflict" indistinguishable from "the
        // server is broken", and only one of those is the operator's to fix.
        var conflicts = preview.ValidationResults.Single(r => r.Rule == "RouteConflicts");
        conflicts.IsValid.Should().BeFalse();
        conflicts.Message.Should().Contain("/api/orders");
        var list = preview.ValidationResults.ToList();
        preview.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task ReportsAnUnsupportedFeatureForTheConfiguredVersion()
    {
        var harness = new Harness();
        var handler = BuildHandler(harness, arrange: h =>
            h.Consistency.Setup(v => v.ValidateGlobalConfiguration(
                    It.IsAny<OcelotVersion>(), It.IsAny<IEnumerable<string>>()))
                .Returns(new[]
                {
                    new ValidationError("CAPABILITY", "claim transformation needs Ocelot 20.0"),
                }));

        var preview = await handler.HandleAsync();

        var capabilities = preview.ValidationResults.Single(r => r.Rule == "OcelotCapabilities");
        capabilities.IsValid.Should().BeFalse();
        capabilities.Message.Should().Contain("20.0");
    }

    [Fact]
    public async Task WarnsRatherThanBlocksWhenThereIsNothingToSnapshot()
    {
        var harness = new Harness();

        var preview = await BuildHandler(harness).HandleAsync();

        // Sealing an empty artifact is occasionally deliberate — clearing a
        // gateway — so it is surfaced and the operator decides.
        preview.ValidationResults.Should().ContainSingle(r => r.Rule == "HasContent");
        preview.IsValid.Should().BeTrue("a warning is not a failure");
    }

    [Fact]
    public async Task TheValidityFlagAgreesWithTheRulesItSummarises()
    {
        // The flag is what the page gates the next step on. It used to be built
        // as "any rule failed", which is the opposite of valid — that let a
        // snapshot through a step the failed rules were meant to block, and it
        // read as "valid" on screen while conflicts were listed underneath.
        var harness = new Harness();
        harness.RouteList.Add(SampleRoute());
        var handler = BuildHandler(harness, arrange: h =>
            h.Conflicts.Setup(d => d.DetectConflicts(
                    It.IsAny<RouteKey>(),
                    It.IsAny<IEnumerable<(RouteId Id, RouteKey Key)>>(),
                    It.IsAny<RouteId?>()))
                .Returns(new[]
                {
                    RouteKey.Create(
                        OcelotHttpMethod.Parse("GET"),
                        UpstreamPath.From("/api/orders"),
                        "localhost"),
                }));

        var preview = await handler.HandleAsync();

        preview.IsValid.Should().Be(
            preview.ValidationResults
                .Where(result => result.Rule != "HasContent")
                .All(result => result.IsValid));
        preview.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task ReportsTheConfiguredOcelotVersionSoThePageCannotClaimAnother()
    {
        var harness = new Harness();

        var preview = await BuildHandler(harness).HandleAsync();

        preview.OcelotVersion.Should().Contain("20");
    }
}
