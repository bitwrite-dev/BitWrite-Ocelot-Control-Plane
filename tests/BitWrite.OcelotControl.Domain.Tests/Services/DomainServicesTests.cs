using FluentAssertions;
using BitWrite.OcelotControl.Domain.Services;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Domain.Exceptions;
using HttpMethod = BitWrite.OcelotControl.Domain.ValueObjects.Configuration.HttpMethod;

namespace BitWrite.OcelotControl.Domain.Tests.Services;

public class RouteConflictDetectorTests
{
    private readonly RouteConflictDetector _detector = new();

    [Fact]
    public void DetectConflicts_ShouldReturnEmpty_WhenNoConflicts()
    {
        var newRoute = RouteKey.Create(HttpMethod.Get, UpstreamPath.From("/api/users"));
        var existingRoutes = new List<(RouteId Id, RouteKey Key)>
        {
            (RouteId.New(), RouteKey.Create(HttpMethod.Get, UpstreamPath.From("/api/orders")))
        };

        var conflicts = _detector.DetectConflicts(newRoute, existingRoutes);

        conflicts.Should().BeEmpty();
    }

    [Fact]
    public void DetectConflicts_ShouldDetectSameMethodAndPath()
    {
        var newRoute = RouteKey.Create(HttpMethod.Get, UpstreamPath.From("/api/users"));
        var existingId = RouteId.New();
        var existingRoutes = new List<(RouteId Id, RouteKey Key)>
        {
            (existingId, RouteKey.Create(HttpMethod.Get, UpstreamPath.From("/api/users")))
        };

        var conflicts = _detector.DetectConflicts(newRoute, existingRoutes);

        conflicts.Should().HaveCount(1);
    }

    [Fact]
    public void DetectConflicts_ShouldExcludeSpecifiedRoute()
    {
        var routeId = RouteId.New();
        var newRoute = RouteKey.Create(HttpMethod.Get, UpstreamPath.From("/api/users"));
        var existingRoutes = new List<(RouteId Id, RouteKey Key)>
        {
            (routeId, RouteKey.Create(HttpMethod.Get, UpstreamPath.From("/api/users")))
        };

        var conflicts = _detector.DetectConflicts(newRoute, existingRoutes, excludeRouteId: routeId);

        conflicts.Should().BeEmpty();
    }

    [Fact]
    public void DetectConflicts_ShouldDetectDifferentMethods()
    {
        var newRoute = RouteKey.Create(HttpMethod.Get, UpstreamPath.From("/api/users"));
        var existingRoutes = new List<(RouteId Id, RouteKey Key)>
        {
            (RouteId.New(), RouteKey.Create(HttpMethod.Post, UpstreamPath.From("/api/users")))
        };

        var conflicts = _detector.DetectConflicts(newRoute, existingRoutes);

        conflicts.Should().BeEmpty();
    }

    [Fact]
    public void AreConflicting_ShouldReturnTrue_ForSameSignature()
    {
        var route1 = RouteKey.Create(HttpMethod.Get, UpstreamPath.From("/api/users"));
        var route2 = RouteKey.Create(HttpMethod.Get, UpstreamPath.From("/api/users"));

        _detector.AreConflicting(route1, route2).Should().BeTrue();
    }

    [Fact]
    public void AreConflicting_ShouldReturnFalse_ForDifferentPaths()
    {
        var route1 = RouteKey.Create(HttpMethod.Get, UpstreamPath.From("/api/users"));
        var route2 = RouteKey.Create(HttpMethod.Get, UpstreamPath.From("/api/orders"));

        _detector.AreConflicting(route1, route2).Should().BeFalse();
    }

    [Fact]
    public void GetRouteSignature_ShouldReturnCorrectFormat()
    {
        var route = RouteKey.Create(HttpMethod.Get, UpstreamPath.From("/api/users"));

        var signature = _detector.GetRouteSignature(route);

        signature.Should().Be("GET:/api/users");
    }

    [Fact]
    public void GetRouteSignature_ShouldIncludeHost_WhenPresent()
    {
        var route = RouteKey.Create(HttpMethod.Get, UpstreamPath.From("/api/users"), "example.com");

        var signature = _detector.GetRouteSignature(route);

        signature.Should().Be("GET:/api/users@example.com");
    }
}

public class ConfigurationConsistencyValidatorTests
{
    private readonly ConfigurationConsistencyValidator _validator = new();

    [Fact]
    public void ValidateServiceReferences_ShouldReturnEmpty_WhenAllServicesExist()
    {
        var serviceIds = new List<ServiceId> { ServiceId.New(), ServiceId.New() };
        var existingIds = serviceIds.ToList();

        var errors = _validator.ValidateServiceReferences(serviceIds, existingIds);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateServiceReferences_ShouldReturnError_WhenServiceNotFound()
    {
        var missingId = ServiceId.New();
        var serviceIds = new List<ServiceId> { missingId };
        var existingIds = new List<ServiceId> { ServiceId.New() };

        var errors = _validator.ValidateServiceReferences(serviceIds, existingIds);

        errors.Should().HaveCount(1);
        errors[0].Code.Should().Be("SERVICE_NOT_FOUND");
    }

    [Fact]
    public void ValidateDownstreamTargets_ShouldReturnEmpty_WhenValid()
    {
        var targets = new List<DownstreamTarget>
        {
            DownstreamTarget.Create("http", "localhost", 5001)
        };

        var errors = _validator.ValidateDownstreamTargets(targets);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateRouteConfiguration_ShouldReturnEmpty_WhenValid()
    {
        var routeId = RouteId.New();
        var serviceId = ServiceId.New();
        var path = UpstreamPath.From("/api/users");
        var targets = new List<DownstreamTarget>
        {
            DownstreamTarget.Create("http", "localhost", 5001)
        };

        var errors = _validator.ValidateRouteConfiguration(routeId, serviceId, path, targets);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateRouteConfiguration_ShouldReturnError_WhenNoTargets()
    {
        var routeId = RouteId.New();
        var serviceId = ServiceId.New();
        var path = UpstreamPath.From("/api/users");
        var targets = new List<DownstreamTarget>();

        var errors = _validator.ValidateRouteConfiguration(routeId, serviceId, path, targets);

        errors.Should().NotBeEmpty();
        errors[0].Code.Should().Be("NO_DOWNSTREAM_TARGETS");
    }

    [Fact]
    public void ValidateGlobalConfiguration_ShouldReturnEmpty_WhenFeaturesSupported()
    {
        var version = OcelotVersion.V19_0;
        var features = new List<string> { "grpc", "jwt" };

        var errors = _validator.ValidateGlobalConfiguration(version, features);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateGlobalConfiguration_ShouldReturnError_WhenFeatureNotSupported()
    {
        var version = OcelotVersion.V18_0;
        var features = new List<string> { "grpc" };

        var errors = _validator.ValidateGlobalConfiguration(version, features);

        errors.Should().NotBeEmpty();
        errors[0].Code.Should().Be("FEATURE_NOT_SUPPORTED");
    }
}

public class ConfigurationBuilderTests
{
    private readonly ConfigurationCanonicalizer _canonicalizer = new();
    private readonly ConfigurationBuilder _builder;

    public ConfigurationBuilderTests()
    {
        _builder = new ConfigurationBuilder(_canonicalizer);
    }

    [Fact]
    public void BuildConfiguration_ShouldCreateValidConfiguration()
    {
        var routes = new List<RouteConfiguration>
        {
            new()
            {
                Id = RouteId.New(),
                Method = HttpMethod.Get,
                UpstreamPath = UpstreamPath.From("/api/users"),
                ServiceId = ServiceId.New(),
                DownstreamTargets = new List<DownstreamTarget>
                {
                    DownstreamTarget.Create("http", "localhost", 5001)
                }
            }
        };
        var globalConfig = new GlobalConfiguration
        {
            BaseUrl = "http://localhost:5000"
        };

        var config = _builder.BuildConfiguration(routes, globalConfig, OcelotVersion.V18_0);

        config.Routes.Should().HaveCount(1);
        config.GlobalConfiguration.BaseUrl.Should().Be("http://localhost:5000");
    }

    [Fact]
    public void CalculateConfigurationHash_ShouldReturnDeterministicHash()
    {
        var routes = new List<RouteConfiguration>
        {
            new()
            {
                Id = RouteId.New(),
                Method = HttpMethod.Get,
                UpstreamPath = UpstreamPath.From("/api/users"),
                ServiceId = ServiceId.New(),
                DownstreamTargets = new List<DownstreamTarget>
                {
                    DownstreamTarget.Create("http", "localhost", 5001)
                }
            }
        };
        var globalConfig = new GlobalConfiguration();

        var config1 = _builder.BuildConfiguration(routes, globalConfig, OcelotVersion.V18_0);
        var config2 = _builder.BuildConfiguration(routes, globalConfig, OcelotVersion.V18_0);

        var hash1 = _builder.CalculateConfigurationHash(config1);
        var hash2 = _builder.CalculateConfigurationHash(config2);

        hash1.Should().Be(hash2);
    }
}

public class OcelotCapabilityResolverTests
{
    private readonly OcelotCapabilityResolver _resolver = new();

    [Fact]
    public void IsCapabilitySupported_ShouldReturnTrue_ForSupportedCapability()
    {
        _resolver.IsCapabilitySupported("http", OcelotVersion.V18_0).Should().BeTrue();
    }

    [Fact]
    public void IsCapabilitySupported_ShouldReturnFalse_ForUnsupportedCapability()
    {
        _resolver.IsCapabilitySupported("grpc", OcelotVersion.V18_0).Should().BeFalse();
    }

    [Fact]
    public void IsCapabilitySupported_ShouldReturnTrue_ForNewerVersion()
    {
        _resolver.IsCapabilitySupported("grpc", OcelotVersion.V19_0).Should().BeTrue();
    }

    [Fact]
    public void GetSupportedCapabilities_ShouldReturnCorrectCapabilities()
    {
        var capabilities = _resolver.GetSupportedCapabilities(OcelotVersion.V18_0);

        capabilities.Should().Contain(c => c.Value == "http");
        capabilities.Should().Contain(c => c.Value == "https");
        capabilities.Should().NotContain(c => c.Value == "grpc");
    }

    [Fact]
    public void GetSupportedCapabilities_ShouldIncludeGrpc_ForV19()
    {
        var capabilities = _resolver.GetSupportedCapabilities(OcelotVersion.V19_0);

        capabilities.Should().Contain(c => c.Value == "grpc");
    }

    [Fact]
    public void GetCapabilityDefinition_ShouldReturnDefinition_WhenExists()
    {
        var definition = _resolver.GetCapabilityDefinition("http");

        definition.Should().NotBeNull();
        definition!.Key.Should().Be("http");
        definition.Name.Should().Be("HTTP Routes");
    }

    [Fact]
    public void GetCapabilityDefinition_ShouldReturnNull_WhenNotExists()
    {
        var definition = _resolver.GetCapabilityDefinition("nonexistent");

        definition.Should().BeNull();
    }

    [Fact]
    public void ResolveEffectiveCapabilities_ShouldCombineVersionAndPluginCapabilities()
    {
        var pluginCapabilities = new List<string> { "custom-auth" };

        var capabilities = _resolver.ResolveEffectiveCapabilities(OcelotVersion.V18_0, pluginCapabilities);

        capabilities.Should().Contain(c => c.Value == "http");
        capabilities.Should().Contain(c => c.Value == "custom-auth");
    }

    [Fact]
    public void ResolveEffectiveCapabilities_ShouldReturnNonEmptySetForVersionUsedByRuntimeAdapter()
    {
        // RuntimeAdapter registers the gateway with these capabilities, and
        // RuntimeInstance.Register rejects an empty set (NO_CAPABILITIES). See #430.
        var capabilities = _resolver.ResolveEffectiveCapabilities(
            OcelotVersion.Parse("20.0.0"), new List<string>());

        capabilities.Should().NotBeEmpty();
        capabilities.Should().OnlyContain(c => !string.IsNullOrWhiteSpace(c.Value));
    }

    [Fact]
    public void ResolveEffectiveCapabilities_ShouldReturnBuiltInCapabilitiesWithNoPlugins()
    {
        var capabilities = _resolver.ResolveEffectiveCapabilities(
            OcelotVersion.V18_0, new List<string>());

        capabilities.Should().NotBeEmpty();
        capabilities.Should().BeEquivalentTo(_resolver.GetSupportedCapabilities(OcelotVersion.V18_0));
    }
}

public class ConfigurationCanonicalizerTests
{
    private readonly ConfigurationCanonicalizer _canonicalizer = new();

    [Fact]
    public void Canonicalize_ShouldReturnDeterministicOutput()
    {
        var config = CreateTestConfiguration();

        var canonical1 = _canonicalizer.Canonicalize(config);
        var canonical2 = _canonicalizer.Canonicalize(config);

        canonical1.Should().Be(canonical2);
    }

    [Fact]
    public void Canonicalize_ShouldBeSorted()
    {
        // Create two configurations with the same routes in different order
        var config1 = new OcelotConfiguration
        {
            GlobalConfiguration = new OcelotGlobalConfiguration { BaseUrl = "http://localhost:5000" },
            Routes = new List<OcelotRouteConfiguration>
            {
                new()
                {
                    UpstreamPathTemplate = "/api/users",
                    UpstreamHttpMethod = new[] { "GET" },
                    DownstreamPathTemplate = "/{everything}",
                    DownstreamScheme = "http"
                },
                new()
                {
                    UpstreamPathTemplate = "/api/orders",
                    UpstreamHttpMethod = new[] { "POST" },
                    DownstreamPathTemplate = "/{everything}",
                    DownstreamScheme = "http"
                }
            }
        };

        var config2 = new OcelotConfiguration
        {
            GlobalConfiguration = new OcelotGlobalConfiguration { BaseUrl = "http://localhost:5000" },
            Routes = new List<OcelotRouteConfiguration>
            {
                new()
                {
                    UpstreamPathTemplate = "/api/orders",
                    UpstreamHttpMethod = new[] { "POST" },
                    DownstreamPathTemplate = "/{everything}",
                    DownstreamScheme = "http"
                },
                new()
                {
                    UpstreamPathTemplate = "/api/users",
                    UpstreamHttpMethod = new[] { "GET" },
                    DownstreamPathTemplate = "/{everything}",
                    DownstreamScheme = "http"
                }
            }
        };

        var canonical1 = _canonicalizer.Canonicalize(config1);
        var canonical2 = _canonicalizer.Canonicalize(config2);

        // The canonicalizer sorts routes by path, so both should produce the same output
        // when they contain the same routes just in different order
        canonical1.Should().Be(canonical2);
    }

    [Fact]
    public void CanonicalizeJson_ShouldReturnValidJson()
    {
        var config = CreateTestConfiguration();

        var json = _canonicalizer.CanonicalizeJson(config);

        json.Should().NotBeNullOrEmpty();
        // Should be valid JSON (we can deserialize it)
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<object>(json);
        deserialized.Should().NotBeNull();
    }

    private OcelotConfiguration CreateTestConfiguration()
    {
        return new OcelotConfiguration
        {
            GlobalConfiguration = new OcelotGlobalConfiguration
            {
                BaseUrl = "http://localhost:5000",
                RequestIdKey = "X-Request-Id"
            },
            Routes = new List<OcelotRouteConfiguration>
            {
                new()
                {
                    UpstreamPathTemplate = "/api/users",
                    UpstreamHttpMethod = new[] { "GET" },
                    DownstreamPathTemplate = "/{everything}",
                    DownstreamScheme = "http",
                    DownstreamHostAndPorts = new List<OcelotHostAndPort>
                    {
                        new() { Host = "localhost", Port = 5001 }
                    }
                }
            }
        };
    }

    private OcelotConfiguration CreateTestConfigurationWithReversedRoutes()
    {
        return new OcelotConfiguration
        {
            GlobalConfiguration = new OcelotGlobalConfiguration
            {
                BaseUrl = "http://localhost:5000",
                RequestIdKey = "X-Request-Id"
            },
            Routes = new List<OcelotRouteConfiguration>
            {
                new()
                {
                    UpstreamPathTemplate = "/api/orders",
                    UpstreamHttpMethod = new[] { "POST" },
                    DownstreamPathTemplate = "/{everything}",
                    DownstreamScheme = "http",
                    DownstreamHostAndPorts = new List<OcelotHostAndPort>
                    {
                        new() { Host = "localhost", Port = 5002 }
                    }
                },
                new()
                {
                    UpstreamPathTemplate = "/api/users",
                    UpstreamHttpMethod = new[] { "GET" },
                    DownstreamPathTemplate = "/{everything}",
                    DownstreamScheme = "http",
                    DownstreamHostAndPorts = new List<OcelotHostAndPort>
                    {
                        new() { Host = "localhost", Port = 5001 }
                    }
                }
            }
        };
    }
}

public class SnapshotIntegrityVerifierTests
{
    private readonly SnapshotIntegrityVerifier _verifier = new(new ConfigurationCanonicalizer());

    [Fact]
    public void VerifyHash_ShouldReturnTrue_WhenHashMatches()
    {
        var content = "test content";
        var hash = _verifier.ComputeHash(content);

        _verifier.VerifyHash(content, hash).Should().BeTrue();
    }

    [Fact]
    public void VerifyHash_ShouldReturnFalse_WhenHashMismatch()
    {
        var content = "test content";
        var wrongContent = "different content";
        var hash = _verifier.ComputeHash(content);

        _verifier.VerifyHash(wrongContent, hash).Should().BeFalse();
    }

    [Fact]
    public void ComputeHash_ShouldReturnConsistentHash()
    {
        var content = "test content";

        var hash1 = _verifier.ComputeHash(content);
        var hash2 = _verifier.ComputeHash(content);

        hash1.Should().Be(hash2);
    }

    [Fact]
    public void VerifyImmutability_ShouldReturnTrue_ForUnchangedContent()
    {
        var content = "snapshot content";
        var originalHash = _verifier.ComputeHash(content);

        _verifier.VerifyImmutability(content, originalHash).Should().BeTrue();
    }

    [Fact]
    public void ValidateIntegrity_ShouldThrow_WhenInvalid()
    {
        var content = "test content";
        var wrongHash = _verifier.ComputeHash("different content");

        var act = () => _verifier.ValidateIntegrity(content, wrongHash, SnapshotVersion.From(1));

        act.Should().Throw<DomainException>()
            .WithMessage("*integrity verification failed*");
    }

    [Fact]
    public void ValidateIntegrity_ShouldNotThrow_WhenValid()
    {
        var content = "test content";
        var hash = _verifier.ComputeHash(content);

        var act = () => _verifier.ValidateIntegrity(content, hash, SnapshotVersion.From(1));

        act.Should().NotThrow();
    }
}

public class SnapshotVersionAllocatorTests
{
    [Fact]
    public void AllocateNext_ShouldIncrementVersion()
    {
        var allocator = new SnapshotVersionAllocator(SnapshotVersion.From(1));

        var next = allocator.AllocateNext();

        next.Value.Should().Be(2);
        allocator.CurrentVersion.Value.Should().Be(2);
    }

    [Fact]
    public void AllocateNext_ShouldBeThreadSafe()
    {
        var allocator = new SnapshotVersionAllocator(SnapshotVersion.From(1));
        var versions = new System.Collections.Concurrent.ConcurrentBag<SnapshotVersion>();

        Parallel.For(0, 100, _ =>
        {
            versions.Add(allocator.AllocateNext());
        });

        versions.Distinct().Count().Should().Be(100);
        allocator.CurrentVersion.Value.Should().Be(101);
    }

    [Fact]
    public void AllocateSpecific_ShouldSetVersion()
    {
        var allocator = new SnapshotVersionAllocator(SnapshotVersion.From(1));

        var allocated = allocator.AllocateSpecific(SnapshotVersion.From(5));

        allocated.Value.Should().Be(5);
        allocator.CurrentVersion.Value.Should().Be(5);
    }

    [Fact]
    public void AllocateSpecific_ShouldThrow_WhenVersionLessThanCurrent()
    {
        var allocator = new SnapshotVersionAllocator(SnapshotVersion.From(5));

        var act = () => allocator.AllocateSpecific(SnapshotVersion.From(3));

        act.Should().Throw<DomainException>()
            .WithMessage("*less than or equal to current version*");
    }

    [Fact]
    public void ResetTo_ShouldSetVersion()
    {
        var allocator = new SnapshotVersionAllocator(SnapshotVersion.From(5));

        allocator.ResetTo(SnapshotVersion.From(1));

        allocator.CurrentVersion.Value.Should().Be(1);
    }
}