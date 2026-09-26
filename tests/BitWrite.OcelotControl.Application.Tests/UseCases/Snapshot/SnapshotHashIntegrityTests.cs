using System.Text.Json;
using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Application.UseCases.Snapshot;
using BitWrite.OcelotControl.Domain.Services;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Domain.ValueObjects.Status;
using FluentAssertions;
using Moq;
using Xunit;
using ValidationError = BitWrite.OcelotControl.Domain.Services.ValidationError;

// The test namespace is `...UseCases.Snapshot`, which shadows the aggregate type.
using SnapshotAggregate = BitWrite.OcelotControl.Domain.Aggregates.Snapshot.Snapshot;
using GlobalConfigAggregate = BitWrite.OcelotControl.Domain.Aggregates.GlobalConfiguration.GlobalConfiguration;
using ServiceIdValue = BitWrite.OcelotControl.Domain.ValueObjects.Identity.ServiceId;
using RouteIdValue = BitWrite.OcelotControl.Domain.ValueObjects.Identity.RouteId;
using RouteAggregate = BitWrite.OcelotControl.Domain.Aggregates.Route.Route;
using ServiceAggregate = BitWrite.OcelotControl.Domain.Aggregates.Service.Service;
using GlobalConfigParam = BitWrite.OcelotControl.Domain.Services.GlobalConfiguration;

namespace BitWrite.OcelotControl.Application.Tests.UseCases.Snapshot;

/// <summary>
/// Guards the invariant that <c>Snapshot.VerifyIntegrity</c> depends on:
/// <c>sha256(snapshot.Content) == snapshot.Hash</c>.
///
/// The handler used to hash the configuration *object* while storing the
/// canonicalised *string*. Those are different formats — <c>Canonicalize()</c>
/// emits a readable text block, <c>CanonicalizeJson()</c> emits JSON — so the
/// two hashes could never be equal and every snapshot failed its own integrity
/// check, which made <c>POST /api/v1/snapshots</c> impossible.
/// </summary>
public class SnapshotHashIntegrityTests
{
    private static OcelotConfiguration SampleConfiguration() => new()
    {
        GlobalConfiguration = new OcelotGlobalConfiguration
        {
            BaseUrl = "http://localhost:5000",
            RequestIdKey = "X-Request-ID",
        },
        Routes = new List<OcelotRouteConfiguration>
        {
            new()
            {
                UpstreamPathTemplate = "/api/orders",
                UpstreamHttpMethod = new[] { "GET" },
                DownstreamHostAndPorts = new List<OcelotHostAndPort>
                {
                    new() { Host = "localhost", Port = 5001 },
                },
            },
        },
    };

    private static CreateSnapshotCommandHandler BuildHandler(Mock<ISnapshotRepository> snapshots)
    {
        // The handler takes the Application interfaces; the adapters live in
        // Infrastructure, so the domain services are wrapped to keep this test
        // inside the Application layer's own seams.
        var canonicalizer = new Mock<IConfigurationCanonicalizer>();
        canonicalizer.Setup(c => c.CanonicalizeJson(It.IsAny<OcelotConfiguration>()))
            .Returns<OcelotConfiguration>(c => new ConfigurationCanonicalizer().CanonicalizeJson(c));

        var integrityVerifier = new Mock<ISnapshotIntegrityVerifier>();
        integrityVerifier.Setup(v => v.ComputeHash(It.IsAny<string>()))
            .Returns<string>(content => new SnapshotIntegrityVerifier(new ConfigurationCanonicalizer())
                .ComputeHash(content));

        var configuration = SampleConfiguration();

        var globalConfig = new Mock<IGlobalConfigurationRepository>();
        globalConfig.Setup(r => r.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(GlobalConfigAggregate.Create());

        var routes = new Mock<IRouteRepository>();
        routes.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RouteAggregate>());

        var services = new Mock<IServiceRepository>();
        services.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ServiceAggregate>());

        var builder = new Mock<IConfigurationBuilder>();
        builder.Setup(b => b.BuildConfiguration(
                It.IsAny<IReadOnlyList<RouteConfiguration>>(),
                It.IsAny<GlobalConfigParam>(),
                It.IsAny<OcelotVersion>()))
            .Returns(configuration);

        var conflicts = new Mock<IRouteConflictDetector>();
        conflicts.Setup(d => d.DetectConflicts(
                It.IsAny<RouteKey>(),
                It.IsAny<IEnumerable<(RouteId Id, RouteKey Key)>>(),
                It.IsAny<RouteIdValue?>()))
            .Returns(Array.Empty<RouteKey>());

        var consistency = new Mock<IConfigurationConsistencyValidator>();
        consistency.Setup(v => v.ValidateServiceReferences(
                It.IsAny<IEnumerable<ServiceIdValue>>(),
                It.IsAny<IEnumerable<ServiceIdValue>>()))
            .Returns(Array.Empty<ValidationError>());
        consistency.Setup(v => v.ValidateDownstreamTargets(It.IsAny<IEnumerable<DownstreamTarget>>()))
            .Returns(Array.Empty<ValidationError>());
        consistency.Setup(v => v.ValidateGlobalConfiguration(
                It.IsAny<OcelotVersion>(), It.IsAny<IEnumerable<string>>()))
            .Returns(Array.Empty<ValidationError>());

        var capabilities = new Mock<IOcelotCapabilityResolver>();
        capabilities.Setup(r => r.GetSupportedCapabilities(It.IsAny<OcelotVersion>()))
            .Returns(Array.Empty<CapabilityKey>());

        var allocator = new Mock<ISnapshotVersionAllocator>();
        allocator.Setup(a => a.AllocateNext()).Returns(SnapshotVersion.From(7));

        var dispatcher = new Mock<IDomainEventDispatcher>();

        return new CreateSnapshotCommandHandler(
            globalConfig.Object, routes.Object, services.Object, snapshots.Object,
            builder.Object, conflicts.Object, consistency.Object,
            capabilities.Object, canonicalizer.Object, integrityVerifier.Object, allocator.Object,
            dispatcher.Object);
    }

    private static SnapshotAggregate PersistedSnapshot(Mock<ISnapshotRepository> snapshots) =>
        (SnapshotAggregate)snapshots.Invocations
            .Single(i => i.Method.Name == nameof(ISnapshotRepository.AddAsync))
            .Arguments[0]!;

    [Fact]
    public async Task CreatedSnapshot_ShouldStoreAHashThatMatchesItsContent()
    {
        var snapshots = new Mock<ISnapshotRepository>();
        var handler = BuildHandler(snapshots);

        await handler.HandleAsync(new CreateSnapshotCommand("admin"));

        var persisted = PersistedSnapshot(snapshots);

        // The invariant the repository, RuntimeAdapter and publish flow rely on.
        var verifier = new SnapshotIntegrityVerifier(new ConfigurationCanonicalizer());
        persisted.VerifyIntegrity(verifier.ComputeHash(persisted.Content)).Should().BeTrue();
    }

    [Fact]
    public async Task CreatedSnapshot_ShouldNotStoreTheObjectLevelConfigurationHash()
    {
        var snapshots = new Mock<ISnapshotRepository>();
        var handler = BuildHandler(snapshots);

        await handler.HandleAsync(new CreateSnapshotCommand("admin"));

        var persisted = PersistedSnapshot(snapshots);
        var canonicalizer = new ConfigurationCanonicalizer();
        var objectLevelHash = new ConfigurationBuilder(canonicalizer)
            .CalculateConfigurationHash(SampleConfiguration());

        // Guards against the original bug returning: the stored hash must not be
        // the one derived from Canonicalize(object).
        persisted.Hash.Value.Should().NotBe(objectLevelHash.Value);
    }

    [Fact]
    public async Task CreatedSnapshot_ShouldSurviveAJsonRoundTripUnchanged()
    {
        var snapshots = new Mock<ISnapshotRepository>();
        var handler = BuildHandler(snapshots);

        await handler.HandleAsync(new CreateSnapshotCommand("admin"));

        var persisted = PersistedSnapshot(snapshots);

        // The repository stores Content as a JSON string, so a round-trip must not
        // alter it — otherwise the stored hash would stop matching.
        var roundTripped = JsonSerializer.Deserialize<JsonElement>(persisted.Content);
        roundTripped.GetRawText().Should().Be(persisted.Content);
    }

    [Fact]
    public async Task CreatedSnapshot_ShouldStoreTheCanonicalisedJsonAsContent()
    {
        var snapshots = new Mock<ISnapshotRepository>();
        var handler = BuildHandler(snapshots);

        await handler.HandleAsync(new CreateSnapshotCommand("admin"));

        var persisted = PersistedSnapshot(snapshots);
        var canonicalizer = new ConfigurationCanonicalizer();
        var expected = canonicalizer.CanonicalizeJson(SampleConfiguration());

        persisted.Content.Should().Be(expected);
    }
}
