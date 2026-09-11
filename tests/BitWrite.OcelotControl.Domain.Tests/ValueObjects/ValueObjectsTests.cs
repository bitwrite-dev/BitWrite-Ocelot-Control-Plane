using FluentAssertions;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.Status;
using BitWrite.OcelotControl.Domain.ValueObjects.FeatureConfig;
using BitWrite.OcelotControl.Domain.Exceptions;
using HttpMethod = BitWrite.OcelotControl.Domain.ValueObjects.Configuration.HttpMethod;

namespace BitWrite.OcelotControl.Domain.Tests.ValueObjects;

public class IdentityValueObjectsTests
{
    [Fact]
    public void RouteId_New_ShouldGenerateNonEmptyId()
    {
        var id = RouteId.New();
        id.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void RouteId_From_ShouldCreateWithValidGuid()
    {
        var guid = Guid.NewGuid();
        var id = RouteId.From(guid);
        id.Value.Should().Be(guid);
    }

    [Fact]
    public void RouteId_From_ShouldThrowOnEmptyGuid()
    {
        var act = () => RouteId.From(Guid.Empty);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void RouteId_FromString_ShouldParseValidGuid()
    {
        var guid = Guid.NewGuid();
        var id = RouteId.From(guid.ToString());
        id.Value.Should().Be(guid);
    }

    [Fact]
    public void RouteId_FromString_ShouldThrowOnInvalidFormat()
    {
        var act = () => RouteId.From("not-a-guid");
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void RouteId_Equality_ShouldBeEqualForSameGuid()
    {
        var guid = Guid.NewGuid();
        var id1 = RouteId.From(guid);
        var id2 = RouteId.From(guid);
        id1.Should().Be(id2);
        id1.GetHashCode().Should().Be(id2.GetHashCode());
    }

    [Fact]
    public void RouteId_Equality_ShouldNotBeEqualForDifferentGuids()
    {
        var id1 = RouteId.New();
        var id2 = RouteId.New();
        id1.Should().NotBe(id2);
    }

    [Fact]
    public void RouteId_ImplicitConversion_ToGuid()
    {
        var guid = Guid.NewGuid();
        var id = RouteId.From(guid);
        Guid result = id;
        result.Should().Be(guid);
    }

    [Fact]
    public void RouteId_ImplicitConversion_ToString()
    {
        var guid = Guid.NewGuid();
        var id = RouteId.From(guid);
        string result = id;
        result.Should().Be(guid.ToString());
    }

    [Fact]
    public void ServiceId_New_ShouldGenerateNonEmptyId()
    {
        var id = ServiceId.New();
        id.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void ServiceId_From_ShouldThrowOnEmptyGuid()
    {
        var act = () => ServiceId.From(Guid.Empty);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void GatewayId_New_ShouldGenerateNonEmptyId()
    {
        var id = GatewayId.New();
        id.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void GatewayId_From_ShouldThrowOnEmptyGuid()
    {
        var act = () => GatewayId.From(Guid.Empty);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void SnapshotVersion_From_ShouldCreateValidVersion()
    {
        var v = SnapshotVersion.From(5);
        v.Value.Should().Be(5);
    }

    [Fact]
    public void SnapshotVersion_From_ShouldThrowOnZero()
    {
        var act = () => SnapshotVersion.From(0);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void SnapshotVersion_From_ShouldThrowOnNegative()
    {
        var act = () => SnapshotVersion.From(-1);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void SnapshotVersion_First_ShouldReturnVersion1()
    {
        var v = SnapshotVersion.First();
        v.Value.Should().Be(1);
    }

    [Fact]
    public void SnapshotVersion_Next_ShouldIncrementByOne()
    {
        var v = SnapshotVersion.From(5);
        var next = v.Next();
        next.Value.Should().Be(6);
    }

    [Fact]
    public void SnapshotVersion_Comparison_ShouldWorkCorrectly()
    {
        var v1 = SnapshotVersion.From(1);
        var v2 = SnapshotVersion.From(2);
        var v3 = SnapshotVersion.From(3);

        (v1 < v2).Should().BeTrue();
        (v2 > v1).Should().BeTrue();
        (v1 <= v1).Should().BeTrue();
        (v3 >= v2).Should().BeTrue();
    }

    [Fact]
    public void PublicationId_New_ShouldGenerateNonEmptyId()
    {
        var id = PublicationId.New();
        id.Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void PublicationId_From_ShouldThrowOnEmptyGuid()
    {
        var act = () => PublicationId.From(Guid.Empty);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void PluginId_From_ShouldNormalizeToLowerCase()
    {
        var id = PluginId.From("My.Plugin");
        id.Value.Should().Be("my.plugin");
    }

    [Fact]
    public void PluginId_From_ShouldThrowOnEmpty()
    {
        var act = () => PluginId.From("");
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void PluginId_From_ShouldThrowOnInvalidFormat()
    {
        var act = () => PluginId.From("My Plugin!");
        act.Should().Throw<DomainException>();
    }
}

public class ConfigurationValueObjectsTests
{
    [Fact]
    public void RouteKey_Create_ShouldBuildKey()
    {
        var method = HttpMethod.Get;
        var path = UpstreamPath.From("/api/v1/users");
        var key = RouteKey.Create(method, path);

        key.Method.Should().Be(method);
        key.Path.Should().Be(path);
        key.Host.Should().BeNull();
    }

    [Fact]
    public void RouteKey_Create_WithHost_ShouldIncludeHost()
    {
        var method = HttpMethod.Post;
        var path = UpstreamPath.From("/api/orders");
        var key = RouteKey.Create(method, path, "example.com");

        key.Host.Should().Be("example.com");
    }

    [Fact]
    public void RouteKey_Parse_ShouldParseValidKey()
    {
        var key = RouteKey.Parse("GET:/api/v1/users");
        key.Method.Value.Should().Be("GET");
        key.Path.Value.Should().Be("/api/v1/users");
    }

    [Fact]
    public void RouteKey_Parse_ShouldThrowOnEmpty()
    {
        var act = () => RouteKey.Parse("");
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void RouteKey_Parse_ShouldThrowOnInvalidFormat()
    {
        var act = () => RouteKey.Parse("INVALID_FORMAT");
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void UpstreamPath_From_ShouldNormalizePath()
    {
        var path = UpstreamPath.From("api/v1/users");
        path.Value.Should().Be("/api/v1/users");
    }

    [Fact]
    public void UpstreamPath_From_ShouldThrowOnEmpty()
    {
        var act = () => UpstreamPath.From("");
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void UpstreamPath_Append_ShouldAppendSegment()
    {
        var path = UpstreamPath.From("/api/v1");
        var appended = path.Append("users");
        appended.Value.Should().Be("/api/v1/users");
    }

    [Fact]
    public void HttpMethod_Parse_ShouldParseValidMethod()
    {
        var method = HttpMethod.Parse("get");
        method.Value.Should().Be("GET");
    }

    [Fact]
    public void HttpMethod_Parse_ShouldThrowOnInvalid()
    {
        var act = () => HttpMethod.Parse("INVALID");
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void HttpMethod_Any_ShouldMatchAny()
    {
        var any = HttpMethod.Any;
        any.IsAny.Should().BeTrue();
    }

    [Fact]
    public void DownstreamTarget_Create_ShouldBuildTarget()
    {
        var target = DownstreamTarget.Create("http", "localhost", 5001, "/api");
        target.Scheme.Should().Be("http");
        target.Host.Should().Be("localhost");
        target.Port.Should().Be(5001);
        target.Path.Should().Be("/api");
    }

    [Fact]
    public void DownstreamTarget_Create_ShouldThrowOnEmptyScheme()
    {
        var act = () => DownstreamTarget.Create("", "localhost", 5001);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void DownstreamTarget_Create_ShouldThrowOnEmptyHost()
    {
        var act = () => DownstreamTarget.Create("http", "", 5001);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void DownstreamTarget_Create_ShouldThrowOnInvalidPort()
    {
        var act = () => DownstreamTarget.Create("http", "localhost", 0);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void DownstreamTarget_Create_ShouldThrowOnInvalidScheme()
    {
        var act = () => DownstreamTarget.Create("ftp", "localhost", 21);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void DownstreamTarget_ToUri_ShouldBuildUri()
    {
        var target = DownstreamTarget.Create("https", "api.example.com", 443, "/v1");
        target.ToUri().Should().Be("https://api.example.com:443/v1");
    }

    [Fact]
    public void ConfigurationHash_FromBytes_ShouldCreateHash()
    {
        var bytes = new byte[32];
        new Random().NextBytes(bytes);
        var hash = ConfigurationHash.FromBytes(bytes);
        hash.Value.Should().HaveLength(64);
    }

    [Fact]
    public void ConfigurationHash_FromBytes_ShouldThrowOnEmpty()
    {
        var act = () => ConfigurationHash.FromBytes(Array.Empty<byte>());
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void ConfigurationHash_FromString_ShouldCreateHash()
    {
        var hex = "a".PadRight(64, 'a');
        var hash = ConfigurationHash.FromString(hex);
        hash.Value.Should().Be(hex);
    }

    [Fact]
    public void ConfigurationHash_FromString_ShouldThrowOnInvalidFormat()
    {
        var act = () => ConfigurationHash.FromString("not-a-hash");
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void OcelotVersion_Parse_ShouldParseVersion()
    {
        var v = OcelotVersion.Parse("18.0.0");
        v.Major.Should().Be(18);
        v.Minor.Should().Be(0);
        v.Patch.Should().Be(0);
    }

    [Fact]
    public void OcelotVersion_Parse_ShouldParseVersionWithPreRelease()
    {
        var v = OcelotVersion.Parse("18.1.0-beta.1");
        v.Major.Should().Be(18);
        v.Minor.Should().Be(1);
        v.Patch.Should().Be(0);
        v.PreRelease.Should().Be("beta.1");
    }

    [Fact]
    public void OcelotVersion_Parse_ShouldThrowOnEmpty()
    {
        var act = () => OcelotVersion.Parse("");
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void OcelotVersion_Comparison_ShouldWorkCorrectly()
    {
        var v18 = OcelotVersion.V18_0;
        var v19 = OcelotVersion.V19_0;
        var v20 = OcelotVersion.V20_0;

        (v18 < v19).Should().BeTrue();
        (v19 > v18).Should().BeTrue();
        (v19 <= v20).Should().BeTrue();
        (v20 >= v19).Should().BeTrue();
    }

    [Fact]
    public void OcelotVersion_IsAtLeast_ShouldReturnCorrectResult()
    {
        var v18 = OcelotVersion.V18_0;
        var v19 = OcelotVersion.V19_0;

        v19.IsAtLeast(v18).Should().BeTrue();
        v18.IsAtLeast(v19).Should().BeFalse();
    }

    [Fact]
    public void OcelotVersion_SupportsFeature_ShouldReturnCorrectResult()
    {
        var featureRegistry = new Dictionary<string, OcelotVersion>
        {
            ["grpc"] = OcelotVersion.V19_0,
            ["jwt"] = OcelotVersion.V18_0
        };

        OcelotVersion.V18_0.SupportsFeature("jwt", featureRegistry).Should().BeTrue();
        OcelotVersion.V18_0.SupportsFeature("grpc", featureRegistry).Should().BeFalse();
        OcelotVersion.V19_0.SupportsFeature("grpc", featureRegistry).Should().BeTrue();
    }
}

public class StatusValueObjectsTests
{
    [Fact]
    public void SnapshotStatus_From_ShouldReturnCorrectStatus()
    {
        SnapshotStatus.Ready.Value.Should().Be("Ready");
        SnapshotStatus.Published.Value.Should().Be("Published");
        SnapshotStatus.Archived.Value.Should().Be("Archived");
    }

    [Fact]
    public void SnapshotStatus_From_ShouldThrowOnInvalidStatus()
    {
        var act = () => SnapshotStatus.From("Invalid");
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void SnapshotStatus_CanTransitionTo_ShouldAllowValidTransitions()
    {
        SnapshotStatus.Ready.CanTransitionTo(SnapshotStatus.Published).Should().BeTrue();
        SnapshotStatus.Ready.CanTransitionTo(SnapshotStatus.Archived).Should().BeTrue();
        SnapshotStatus.Published.CanTransitionTo(SnapshotStatus.Archived).Should().BeTrue();
    }

    [Fact]
    public void SnapshotStatus_CanTransitionTo_ShouldDisallowInvalidTransitions()
    {
        SnapshotStatus.Published.CanTransitionTo(SnapshotStatus.Ready).Should().BeFalse();
        SnapshotStatus.Archived.CanTransitionTo(SnapshotStatus.Ready).Should().BeFalse();
        SnapshotStatus.Archived.CanTransitionTo(SnapshotStatus.Published).Should().BeFalse();
    }

    [Fact]
    public void PublicationStatus_From_ShouldReturnCorrectStatus()
    {
        PublicationStatus.Pending.Value.Should().Be("Pending");
        PublicationStatus.Published.Value.Should().Be("Published");
        PublicationStatus.Failed.Value.Should().Be("Failed");
        PublicationStatus.RolledBack.Value.Should().Be("RolledBack");
    }

    [Fact]
    public void PublicationStatus_From_ShouldThrowOnInvalidStatus()
    {
        var act = () => PublicationStatus.From("Invalid");
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void PublicationStatus_IsTerminal_ShouldReturnCorrectResult()
    {
        PublicationStatus.Pending.IsTerminal.Should().BeFalse();
        PublicationStatus.Published.IsTerminal.Should().BeTrue();
        PublicationStatus.Failed.IsTerminal.Should().BeTrue();
        PublicationStatus.RolledBack.IsTerminal.Should().BeTrue();
    }

    [Fact]
    public void RuntimeStatus_From_ShouldReturnCorrectStatus()
    {
        RuntimeStatus.Disconnected.Value.Should().Be("Disconnected");
        RuntimeStatus.Connecting.Value.Should().Be("Connecting");
        RuntimeStatus.Synchronized.Value.Should().Be("Synchronized");
        RuntimeStatus.Applying.Value.Should().Be("Applying");
        RuntimeStatus.Active.Value.Should().Be("Active");
        RuntimeStatus.Degraded.Value.Should().Be("Degraded");
    }

    [Fact]
    public void RuntimeStatus_From_ShouldThrowOnInvalidStatus()
    {
        var act = () => RuntimeStatus.From("Invalid");
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void RuntimeStatus_IsHealthy_ShouldReturnCorrectResult()
    {
        RuntimeStatus.Synchronized.IsHealthy.Should().BeTrue();
        RuntimeStatus.Active.IsHealthy.Should().BeTrue();
        RuntimeStatus.Disconnected.IsHealthy.Should().BeFalse();
        RuntimeStatus.Degraded.IsHealthy.Should().BeFalse();
    }

    [Fact]
    public void RuntimeStatus_IsDegraded_ShouldReturnCorrectResult()
    {
        RuntimeStatus.Degraded.IsDegraded.Should().BeTrue();
        RuntimeStatus.Active.IsDegraded.Should().BeFalse();
    }

    [Fact]
    public void PluginScope_From_ShouldReturnCorrectScope()
    {
        PluginScope.Global.Value.Should().Be("Global");
        PluginScope.Route.Value.Should().Be("Route");
    }

    [Fact]
    public void PluginScope_From_ShouldThrowOnInvalidScope()
    {
        var act = () => PluginScope.From("Invalid");
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void CapabilityKey_From_ShouldNormalizeToLowerCase()
    {
        var key = CapabilityKey.From("My.Capability");
        key.Value.Should().Be("my.capability");
    }

    [Fact]
    public void CapabilityKey_From_ShouldThrowOnEmpty()
    {
        var act = () => CapabilityKey.From("");
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void CapabilityKey_From_ShouldThrowOnInvalidFormat()
    {
        var act = () => CapabilityKey.From("My Capability!");
        act.Should().Throw<DomainException>();
    }
}

public class FeatureConfigValueObjectsTests
{
    [Fact]
    public void AuthenticationOptions_Create_ShouldBuildOptions()
    {
        var options = AuthenticationOptions.Create("Bearer", "IdentityServer");
        options.Scheme.Should().Be("Bearer");
        options.Provider.Should().Be("IdentityServer");
        options.Properties.Should().BeEmpty();
    }

    [Fact]
    public void AuthenticationOptions_Create_WithProperties_ShouldIncludeProperties()
    {
        var props = new Dictionary<string, string> { ["issuer"] = "https://auth.example.com" };
        var options = AuthenticationOptions.Create("Bearer", "IdentityServer", props);
        options.Properties.Should().ContainKey("issuer");
    }

    [Fact]
    public void AuthenticationOptions_Create_ShouldThrowOnEmptyScheme()
    {
        var act = () => AuthenticationOptions.Create("");
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void AuthenticationOptions_None_ShouldReturnNull()
    {
        AuthenticationOptions.None().Should().BeNull();
    }

    [Fact]
    public void AuthorizationOptions_Create_ShouldBuildOptions()
    {
        var options = AuthorizationOptions.Create(
            policies: new List<string> { "Admin" },
            scopes: new List<string> { "read", "write" }
        );
        options.Policies.Should().Contain("Admin");
        options.Scopes.Should().Contain("read");
    }

    [Fact]
    public void AuthorizationOptions_HasPolicy_ShouldReturnCorrectResult()
    {
        var options = AuthorizationOptions.Create(policies: new List<string> { "Admin" });
        options.HasPolicy("Admin").Should().BeTrue();
        options.HasPolicy("User").Should().BeFalse();
    }

    [Fact]
    public void AuthorizationOptions_HasScope_ShouldReturnCorrectResult()
    {
        var options = AuthorizationOptions.Create(scopes: new List<string> { "read" });
        options.HasScope("read").Should().BeTrue();
        options.HasScope("write").Should().BeFalse();
    }

    [Fact]
    public void RateLimitOptions_Create_ShouldBuildOptions()
    {
        var options = RateLimitOptions.Create(100, "Minute");
        options.Limit.Should().Be(100);
        options.Period.Should().Be("Minute");
        options.PeriodSeconds.Should().Be(60);
    }

    [Fact]
    public void RateLimitOptions_Create_ShouldThrowOnInvalidLimit()
    {
        var act = () => RateLimitOptions.Create(0, "Minute");
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void RateLimitOptions_Create_ShouldThrowOnInvalidPeriod()
    {
        var act = () => RateLimitOptions.Create(100, "Invalid");
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void RateLimitOptions_None_ShouldReturnNull()
    {
        RateLimitOptions.None().Should().BeNull();
    }

    [Fact]
    public void QoSOptions_Create_ShouldBuildOptions()
    {
        var options = QoSOptions.Create(timeoutSeconds: 30, retryCount: 3);
        options.TimeoutSeconds.Should().Be(30);
        options.RetryCount.Should().Be(3);
    }

    [Fact]
    public void QoSOptions_Create_ShouldThrowOnInvalidTimeout()
    {
        var act = () => QoSOptions.Create(timeoutSeconds: -1);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void QoSOptions_Create_ShouldThrowOnInvalidRetryCount()
    {
        var act = () => QoSOptions.Create(retryCount: -1);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void QoSOptions_None_ShouldReturnNull()
    {
        QoSOptions.None().Should().BeNull();
    }

    [Fact]
    public void CacheOptions_Create_ShouldBuildOptions()
    {
        var options = CacheOptions.Create(60);
        options.TtlSeconds.Should().Be(60);
    }

    [Fact]
    public void CacheOptions_Create_ShouldThrowOnInvalidTtl()
    {
        var act = () => CacheOptions.Create(0);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void CacheOptions_None_ShouldReturnNull()
    {
        CacheOptions.None().Should().BeNull();
    }

    [Fact]
    public void LoadBalancerOptions_RoundRobin_ShouldCreateCorrectly()
    {
        var options = LoadBalancerOptions.RoundRobin();
        options.Algorithm.Should().Be("RoundRobin");
    }

    [Fact]
    public void LoadBalancerOptions_LeastConnection_ShouldCreateCorrectly()
    {
        var options = LoadBalancerOptions.LeastConnection();
        options.Algorithm.Should().Be("LeastConnection");
    }

    [Fact]
    public void LoadBalancerOptions_NoLoadBalancer_ShouldCreateCorrectly()
    {
        var options = LoadBalancerOptions.NoLoadBalancer();
        options.Algorithm.Should().Be("NoLoadBalancer");
    }

    [Fact]
    public void LoadBalancerOptions_CookieStickySession_ShouldCreateCorrectly()
    {
        var options = LoadBalancerOptions.CookieStickySession();
        options.Algorithm.Should().Be("CookieStickySession");
    }

    [Fact]
    public void LoadBalancerOptions_Create_ShouldThrowOnInvalidAlgorithm()
    {
        var act = () => LoadBalancerOptions.Create("Invalid");
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void LoadBalancerOptions_None_ShouldReturnNull()
    {
        LoadBalancerOptions.None().Should().BeNull();
    }

    [Fact]
    public void HeaderOptions_Create_ShouldBuildOptions()
    {
        var adds = new List<HeaderTransform> { HeaderTransform.Create("X-Custom", "value") };
        var removes = new List<string> { "X-Remove" };
        var options = HeaderOptions.Create(adds, removes);
        options.Add.Should().HaveCount(1);
        options.Remove.Should().HaveCount(1);
    }

    [Fact]
    public void HeaderOptions_None_ShouldReturnNull()
    {
        HeaderOptions.None().Should().BeNull();
    }

    [Fact]
    public void HeaderTransform_Create_ShouldBuildTransform()
    {
        var transform = HeaderTransform.Create("X-Custom", "value");
        transform.Key.Should().Be("X-Custom");
        transform.Value.Should().Be("value");
    }

    [Fact]
    public void HeaderTransform_Create_ShouldThrowOnEmptyKey()
    {
        var act = () => HeaderTransform.Create("", "value");
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void ClaimOptions_Create_ShouldBuildOptions()
    {
        var adds = new List<ClaimTransform> { ClaimTransform.Create("role", "admin") };
        var options = ClaimOptions.Create(adds);
        options.Add.Should().HaveCount(1);
    }

    [Fact]
    public void ClaimOptions_None_ShouldReturnNull()
    {
        ClaimOptions.None().Should().BeNull();
    }

    [Fact]
    public void ClaimTransform_Create_ShouldBuildTransform()
    {
        var transform = ClaimTransform.Create("role", "admin");
        transform.Key.Should().Be("role");
        transform.Value.Should().Be("admin");
    }

    [Fact]
    public void ClaimTransform_Create_ShouldThrowOnEmptyKey()
    {
        var act = () => ClaimTransform.Create("", "admin");
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void QueryOptions_Create_ShouldBuildOptions()
    {
        var adds = new List<QueryTransform> { QueryTransform.Create("version", "v1") };
        var options = QueryOptions.Create(adds);
        options.Add.Should().HaveCount(1);
    }

    [Fact]
    public void QueryOptions_None_ShouldReturnNull()
    {
        QueryOptions.None().Should().BeNull();
    }

    [Fact]
    public void QueryTransform_Create_ShouldBuildTransform()
    {
        var transform = QueryTransform.Create("version", "v1");
        transform.Key.Should().Be("version");
        transform.Value.Should().Be("v1");
    }

    [Fact]
    public void QueryTransform_Create_ShouldThrowOnEmptyKey()
    {
        var act = () => QueryTransform.Create("", "v1");
        act.Should().Throw<DomainException>();
    }
}