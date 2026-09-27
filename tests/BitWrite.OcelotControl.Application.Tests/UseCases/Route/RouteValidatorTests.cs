using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Application.UseCases.Route;
using DomainRoute = BitWrite.OcelotControl.Domain.Aggregates.Route.Route;
using DomainService = BitWrite.OcelotControl.Domain.Aggregates.Service.Service;
using BitWrite.OcelotControl.Domain.Services;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using FluentAssertions;
using Moq;
using Xunit;
using MinimalGlobalConfig = BitWrite.OcelotControl.Domain.Services.GlobalConfiguration;
using DomainHttpMethod = BitWrite.OcelotControl.Domain.ValueObjects.Configuration.HttpMethod;
using DomainValidationError = BitWrite.OcelotControl.Domain.Services.ValidationError;
using DomainGlobalConfig = BitWrite.OcelotControl.Domain.Services.GlobalConfiguration;
using DomainConfigurationHash = BitWrite.OcelotControl.Domain.ValueObjects.Configuration.ConfigurationHash;

namespace BitWrite.OcelotControl.Application.Tests.UseCases.Route;

public class RouteValidatorTests
{
    private readonly Mock<IRouteRepository> _routeRepository;
    private readonly Mock<IServiceRepository> _serviceRepository;
    private readonly Mock<IConfigurationBuilder> _configurationBuilder;
    private readonly Mock<IRouteConflictDetector> _conflictDetector;
    private readonly Mock<IConfigurationConsistencyValidator> _consistencyValidator;
    private readonly Mock<IConfigurationCanonicalizer> _canonicalizer;
    private readonly Mock<ISnapshotIntegrityVerifier> _integrityVerifier;
    private readonly RouteValidator _validator;

    public RouteValidatorTests()
    {
        _routeRepository = new Mock<IRouteRepository>();
        _serviceRepository = new Mock<IServiceRepository>();
        _configurationBuilder = new Mock<IConfigurationBuilder>();
        _conflictDetector = new Mock<IRouteConflictDetector>();
        _consistencyValidator = new Mock<IConfigurationConsistencyValidator>();
        _canonicalizer = new Mock<IConfigurationCanonicalizer>();
        _integrityVerifier = new Mock<ISnapshotIntegrityVerifier>();

        // Defaults for a route with nothing wrong with it.
        _routeRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DomainRoute>());
        _conflictDetector.Setup(d => d.DetectConflicts(
                It.IsAny<RouteKey>(),
                It.IsAny<IEnumerable<(RouteId Id, RouteKey Key)>>(),
                It.IsAny<RouteId?>()))
            .Returns(Array.Empty<RouteKey>());
        _consistencyValidator.Setup(v => v.ValidateDownstreamTargets(It.IsAny<IEnumerable<DownstreamTarget>>()))
            .Returns(Array.Empty<DomainValidationError>());
        _consistencyValidator.Setup(v => v.ValidateGlobalConfiguration(
                It.IsAny<OcelotVersion>(), It.IsAny<IEnumerable<string>>()))
            .Returns(Array.Empty<DomainValidationError>());
        _configurationBuilder.Setup(b => b.BuildConfiguration(
                It.IsAny<IReadOnlyList<RouteConfiguration>>(),
                It.IsAny<DomainGlobalConfig>(),
                It.IsAny<OcelotVersion>()))
            .Returns(new OcelotConfiguration());
        _canonicalizer.Setup(c => c.CanonicalizeJson(It.IsAny<OcelotConfiguration>()))
            .Returns("{}");
        _integrityVerifier.Setup(v => v.ComputeHash(It.IsAny<string>()))
            .Returns(DomainConfigurationHash.FromString(new string('a', 64)));

        _validator = new RouteValidator(
            _routeRepository.Object,
            _serviceRepository.Object,
            _configurationBuilder.Object,
            _conflictDetector.Object,
            _consistencyValidator.Object,
            _canonicalizer.Object,
            _integrityVerifier.Object);
    }

    private RouteValidationInput ValidInput(
        RouteId? existingId = null,
        IReadOnlyList<string>? features = null) =>
        new(
            new RouteConfiguration
            {
                Id = default!,
                Host = null,
                Method = DomainHttpMethod.Get,
                UpstreamPath = UpstreamPath.From("/api/test"),
                ServiceId = ServiceId.From(Guid.NewGuid()),
                DownstreamTargets = new List<DownstreamTarget>
                {
                    DownstreamTarget.Create("http", "localhost", 5001)
                }
            },
            RouteKey.Create(DomainHttpMethod.Get, UpstreamPath.From("/api/test")),
            existingId,
            features ?? new List<string>());

    private void ServiceExists(ServiceId id) =>
        _serviceRepository.Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DomainService.Create("svc"));

    [Fact]
    public async Task ValidateAsync_ShouldBeValid_WhenNothingIsWrong()
    {
        var input = ValidInput();
        ServiceExists(input.Configuration.ServiceId);

        var result = await _validator.ValidateAsync(input);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateAsync_ShouldTagAMissingServiceWithItsField()
    {
        var input = ValidInput();
        // No service registered, so the lookup returns null.

        var result = await _validator.ValidateAsync(input);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
            .Which.Should().Match<RouteValidationError>(e =>
                e.Field == "serviceId" && e.Code == "SERVICE_NOT_FOUND");
    }

    [Fact]
    public async Task ValidateAsync_ShouldTagAConflictWithTheKeyField()
    {
        var input = ValidInput();
        ServiceExists(input.Configuration.ServiceId);
        _conflictDetector.Setup(d => d.DetectConflicts(
                It.IsAny<RouteKey>(),
                It.IsAny<IEnumerable<(RouteId Id, RouteKey Key)>>(),
                It.IsAny<RouteId?>()))
            .Returns(new[] { input.Key });

        var result = await _validator.ValidateAsync(input);

        result.Errors.Should().ContainSingle()
            .Which.Should().Match<RouteValidationError>(e => e.Field == "key" && e.Code == "ROUTE_CONFLICT");
    }

    [Fact]
    public async Task ValidateAsync_ShouldTagDownstreamTargetProblems()
    {
        var input = ValidInput();
        ServiceExists(input.Configuration.ServiceId);
        _consistencyValidator.Setup(v => v.ValidateDownstreamTargets(It.IsAny<IEnumerable<DownstreamTarget>>()))
            .Returns(new[] { new DomainValidationError("NO_TARGETS", "at least one target is required") });

        var result = await _validator.ValidateAsync(input);

        result.Errors.Should().ContainSingle()
            .Which.Should().Match<RouteValidationError>(e =>
                e.Field == "downstreamTargets" && e.Code == "NO_TARGETS");
    }

    [Fact]
    public async Task ValidateAsync_ShouldPointACapabilityFailureAtTheFieldThatEnabledIt()
    {
        var input = ValidInput(features: new List<string> { "rate-limiting" });
        ServiceExists(input.Configuration.ServiceId);
        _consistencyValidator.Setup(v => v.ValidateGlobalConfiguration(
                It.IsAny<OcelotVersion>(), It.IsAny<IEnumerable<string>>()))
            .Returns(new[]
            {
                new DomainValidationError("UNSUPPORTED_CAPABILITY", "rate-limiting is not supported")
            });

        var result = await _validator.ValidateAsync(input);

        result.Errors.Should().ContainSingle()
            .Which.Field.Should().Be("rateLimitOptions");
    }

    [Fact]
    public async Task ValidateAsync_ShouldLeaveWholeRouteProblemsWithoutAField()
    {
        var input = ValidInput();
        ServiceExists(input.Configuration.ServiceId);
        _canonicalizer.Setup(c => c.CanonicalizeJson(It.IsAny<OcelotConfiguration>()))
            .Throws(new InvalidOperationException("bad shape"));

        var result = await _validator.ValidateAsync(input);

        result.Errors.Should().ContainSingle()
            .Which.Should().Match<RouteValidationError>(e =>
                e.Field == null && e.Code == "CONFIGURATION_INVALID");
    }

    [Fact]
    public async Task ValidateAsync_ShouldPassTheDraftIdThroughAsNull()
    {
        // A draft is not stored, so nothing should be excluded from conflicts.
        var input = ValidInput(existingId: null);
        ServiceExists(input.Configuration.ServiceId);

        await _validator.ValidateAsync(input);

        _conflictDetector.Verify(d => d.DetectConflicts(
            It.IsAny<RouteKey>(),
            It.IsAny<IEnumerable<(RouteId Id, RouteKey Key)>>(),
            null));
    }

    [Fact]
    public async Task ValidateAsync_ShouldExcludeAStoredRouteFromItsOwnConflicts()
    {
        var input = ValidInput(existingId: RouteId.New());
        ServiceExists(input.Configuration.ServiceId);

        await _validator.ValidateAsync(input);

        _conflictDetector.Verify(d => d.DetectConflicts(
            It.IsAny<RouteKey>(),
            It.IsAny<IEnumerable<(RouteId Id, RouteKey Key)>>(),
            input.ExistingRouteId));
    }

    [Fact]
    public async Task ValidateAsync_ShouldPersistNothing()
    {
        // The whole point of a draft check: no writes, no events.
        var input = ValidInput();
        ServiceExists(input.Configuration.ServiceId);

        await _validator.ValidateAsync(input);

        _routeRepository.Verify(
            r => r.AddAsync(It.IsAny<DomainRoute>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _routeRepository.Verify(
            r => r.UpdateAsync(It.IsAny<DomainRoute>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
