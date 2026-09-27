using FluentAssertions;
using BitWrite.OcelotControl.Domain.Aggregates.Route;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.FeatureConfig;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Domain.Events;
using BitWrite.OcelotControl.Domain.Exceptions;
using HttpMethod = BitWrite.OcelotControl.Domain.ValueObjects.Configuration.HttpMethod;

namespace BitWrite.OcelotControl.Domain.Tests.Aggregates;

/// <summary>
/// The replace operation behind PUT /api/v1/routes/{id}.
/// </summary>
public class RouteReplaceTests
{
    private static Route CreateTestRoute() =>
        Route.Create(
            HttpMethod.Get,
            UpstreamPath.From("/api/test"),
            ServiceId.New(),
            new List<DownstreamTarget> { DownstreamTarget.Create("http", "localhost", 5001) },
            "key",
            "api.example.com");

    private static List<DownstreamTarget> OneTarget(int port = 5002) =>
        new() { DownstreamTarget.Create("http", "localhost", port) };

    [Fact]
    public void Replace_ShouldApplyEveryField()
    {
        var route = CreateTestRoute();
        var serviceId = ServiceId.New();

        route.Replace(
            HttpMethod.Post,
            UpstreamPath.From("/api/updated"),
            serviceId,
            OneTarget(6000),
            "updated",
            "other.example.com",
            null, null, null, null, null);

        route.Method.Should().Be(HttpMethod.Post);
        route.UpstreamPath.Should().Be(UpstreamPath.From("/api/updated"));
        route.ServiceId.Should().Be(serviceId);
        route.Key.Should().Be("updated");
        route.Host.Should().Be("other.example.com");
        route.DownstreamTargets.Should().ContainSingle()
            .Which.Port.Should().Be(6000);
    }

    [Fact]
    public void Replace_ShouldRetargetTheRoute()
    {
        // The case the old handler dropped on the floor: a new set of targets
        // that shares nothing with the old one.
        var route = CreateTestRoute();
        route.AddDownstreamTarget(DownstreamTarget.Create("http", "localhost", 5002));

        route.Replace(
            HttpMethod.Get,
            UpstreamPath.From("/api/test"),
            route.ServiceId,
            new List<DownstreamTarget>
            {
                DownstreamTarget.Create("https", "other.internal", 8443),
                DownstreamTarget.Create("https", "third.internal", 8443),
            },
            "key",
            null,
            null, null, null, null, null);

        route.DownstreamTargets.Should().HaveCount(2);
        route.DownstreamTargets.Should().AllSatisfy(t =>
        {
            t.Scheme.Should().Be("https");
            t.Port.Should().Be(8443);
        });
    }

    [Fact]
    public void Replace_ShouldClearTheHost()
    {
        var route = CreateTestRoute();

        route.Replace(
            HttpMethod.Get,
            UpstreamPath.From("/api/test"),
            route.ServiceId,
            OneTarget(),
            "key",
            host: null,
            null, null, null, null, null);

        route.Host.Should().BeNull();
    }

    [Fact]
    public void Replace_ShouldRemoveEveryFeatureBlockWhenNoneAreGiven()
    {
        var route = CreateTestRoute();
        route.SetAuthentication(AuthenticationOptions.Create(
            "Bearer", null, new Dictionary<string, string> { ["scopes"] = "a,b" }));
        route.SetRateLimit(RateLimitOptions.Create(10, "Minute"));
        route.SetQoS(QoSOptions.Create(30));
        route.SetCache(CacheOptions.Create(60));
        route.SetLoadBalancer(LoadBalancerOptions.Create("RoundRobin"));

        route.Replace(
            HttpMethod.Get,
            UpstreamPath.From("/api/test"),
            route.ServiceId,
            OneTarget(),
            "key",
            null,
            null, null, null, null, null);

        route.AuthenticationOptions.Should().BeNull();
        route.RateLimitOptions.Should().BeNull();
        route.QoSOptions.Should().BeNull();
        route.CacheOptions.Should().BeNull();
        route.LoadBalancerOptions.Should().BeNull();
    }

    [Fact]
    public void Replace_ShouldSetAFeatureBlock()
    {
        var route = CreateTestRoute();
        var cache = CacheOptions.Create(120);

        route.Replace(
            HttpMethod.Get,
            UpstreamPath.From("/api/test"),
            route.ServiceId,
            OneTarget(),
            "key",
            null,
            null, null, null, cache, null);

        route.CacheOptions.Should().Be(cache);
    }

    [Fact]
    public void Replace_ShouldKeepOrRemoveEveryFeatureBlock()
    {
        // A replacement has to reach the feature configs too, or editing a route
        // would silently strip its rate limit and cache.
        var route = CreateTestRoute();
        route.SetRateLimit(RateLimitOptions.Create(100, "Minute"));
        route.SetCache(CacheOptions.Create(60));
        route.SetAuthorization(AuthorizationOptions.Create(new List<string> { "Admin" }));
        route.SetHeaders(HeaderOptions.Create(null, new List<string> { "X-Debug" }, null));

        // Keeps the rate limit, drops the rest.
        route.Replace(
            HttpMethod.Get,
            UpstreamPath.From("/api/test"),
            route.ServiceId,
            OneTarget(),
            "key",
            null,
            authenticationOptions: null,
            rateLimitOptions: RateLimitOptions.Create(200, "Hour"),
            qosOptions: null,
            cacheOptions: null,
            loadBalancerOptions: null);

        route.RateLimitOptions.Should().NotBeNull();
        route.RateLimitOptions!.Limit.Should().Be(200);
        route.CacheOptions.Should().BeNull();
        route.AuthorizationOptions.Should().BeNull();
        route.HeaderOptions.Should().BeNull();
    }

    [Fact]
    public void Replace_ShouldRaiseExactlyOneRouteUpdatedEvent()
    {
        var route = CreateTestRoute();
        route.ClearDomainEvents();

        route.Replace(
            HttpMethod.Get,
            UpstreamPath.From("/api/test"),
            route.ServiceId,
            OneTarget(),
            "key",
            null,
            null, null, null, null, null);

        route.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<RouteUpdated>();
    }

    [Fact]
    public void Replace_ShouldRejectAnEmptyTargetList()
    {
        var route = CreateTestRoute();

        var act = () => route.Replace(
            HttpMethod.Get,
            UpstreamPath.From("/api/test"),
            route.ServiceId,
            new List<DownstreamTarget>(),
            "key",
            null,
            null, null, null, null, null);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Replace_ShouldRejectMoreThanTenTargets()
    {
        var route = CreateTestRoute();
        var many = Enumerable
            .Range(0, 11)
            .Select(i => DownstreamTarget.Create("http", $"host{i}", 5000 + i))
            .ToList();

        var act = () => route.Replace(
            HttpMethod.Get,
            UpstreamPath.From("/api/test"),
            route.ServiceId,
            many,
            "key",
            null,
            null, null, null, null, null);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Replace_ShouldRejectDuplicateTargets()
    {
        var route = CreateTestRoute();
        var duplicates = new List<DownstreamTarget>
        {
            DownstreamTarget.Create("http", "localhost", 5001),
            DownstreamTarget.Create("http", "localhost", 5001),
        };

        var act = () => route.Replace(
            HttpMethod.Get,
            UpstreamPath.From("/api/test"),
            route.ServiceId,
            duplicates,
            "key",
            null,
            null, null, null, null, null);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Replace_ShouldLeaveTheRouteUntouched_WhenValidationFails()
    {
        // A rejected replacement must not half-apply.
        var route = CreateTestRoute();
        var originalPath = route.UpstreamPath;

        var act = () => route.Replace(
            HttpMethod.Post,
            UpstreamPath.From("/api/changed"),
            route.ServiceId,
            new List<DownstreamTarget>(),
            "changed",
            null,
            null, null, null, null, null);

        act.Should().Throw<DomainException>();
        route.Method.Should().Be(HttpMethod.Get);
        route.UpstreamPath.Should().Be(originalPath);
        route.Key.Should().Be("key");
    }
}
