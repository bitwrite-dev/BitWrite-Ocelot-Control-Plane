using BitWrite.OcelotControl.Application.UseCases.Route;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.FeatureConfig;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using FluentAssertions;
using Xunit;
using DomainRoute = BitWrite.OcelotControl.Domain.Aggregates.Route.Route;
using HttpMethod = BitWrite.OcelotControl.Domain.ValueObjects.Configuration.HttpMethod;

namespace BitWrite.OcelotControl.Application.Tests.UseCases.Route;

/// <summary>
/// The single aggregate-to-builder projection.
/// </summary>
/// <remarks>
/// It existed in four copies — preview, effective, validation and the snapshot
/// path. They were identical, which is the problem: a new field meant four
/// edits, and the validation copy was already missing the newer blocks, so a
/// route using them was validated against a configuration that did not contain
/// them. In the snapshot copy that fails silently.
/// </remarks>
public class RouteConfigurationMapperTests
{
    private static DomainRoute SampleRoute()
    {
        var route = DomainRoute.Create(
            HttpMethod.Get,
            UpstreamPath.From("/api/users"),
            ServiceId.New(),
            new List<DownstreamTarget> { DownstreamTarget.Create("http", "localhost", 5001) },
            "users-list",
            "api.example.com");

        route.SetAuthentication(AuthenticationOptions.Create(
            "Bearer",
            null,
            new Dictionary<string, string> { ["scopes"] = "users.read" }));

        return route;
    }

    [Fact]
    public void Map_ShouldCarryIdentityAndTargets()
    {
        var route = SampleRoute();

        var input = RouteConfigurationMapper.Map(route);

        input.Id.Should().Be(route.Id);
        input.Host.Should().Be("api.example.com");
        input.Method.Should().Be(HttpMethod.Get);
        input.UpstreamPath.Should().Be(UpstreamPath.From("/api/users"));
        input.ServiceId.Should().Be(route.ServiceId);
        input.DownstreamTargets.Should().ContainSingle()
            .Which.Port.Should().Be(5001);
    }

    [Fact]
    public void Map_ShouldCarryEveryOptionBlock()
    {
        var route = SampleRoute();
        route.SetAuthorization(AuthorizationOptions.Create(
            new List<string> { "RequireAdmin" },
            new List<string> { "admin" },
            new Dictionary<string, string> { ["role"] = "admin" }));
        route.SetRateLimit(RateLimitOptions.Create(250, "Hour"));
        route.SetQoS(QoSOptions.Create(45, 2, true, 10, 3));
        route.SetCache(CacheOptions.Create(300, "k", "region", new List<string> { "Accept" }));
        route.SetLoadBalancer(LoadBalancerOptions.LeastConnection());
        route.SetHeaders(HeaderOptions.Create(
            new List<HeaderTransform> { HeaderTransform.Create("X-Api-Key", "secret") },
            new List<string> { "X-Debug" },
            new List<HeaderTransform> { HeaderTransform.Create("X-Trace", "$1") }));
        route.SetClaims(ClaimOptions.Create(
            new List<ClaimTransform> { ClaimTransform.Create("sub", "user-1") }, null, null));
        route.SetQuery(QueryOptions.Create(
            null, new List<string> { "debug" }, null));

        var input = RouteConfigurationMapper.Map(route);

        input.AuthenticationOptions.Should().NotBeNull();
        input.AuthorizationOptions.Should().NotBeNull();
        input.AuthorizationOptions!.Policies.Should().ContainSingle("RequireAdmin");
        input.RateLimitOptions.Should().NotBeNull();
        input.QoSOptions.Should().NotBeNull();
        input.CacheOptions.Should().NotBeNull();
        input.LoadBalancerOptions.Should().NotBeNull();
        input.HeaderOptions.Should().NotBeNull();
        input.HeaderOptions!.Remove.Should().ContainSingle("X-Debug");
        input.ClaimOptions.Should().NotBeNull();
        input.QueryOptions.Should().NotBeNull();
    }

    [Fact]
    public void Map_ShouldLeaveEveryOptionNull_WhenNoneAreSet()
    {
        var route = DomainRoute.Create(
            HttpMethod.Get,
            UpstreamPath.From("/api/plain"),
            ServiceId.New(),
            new List<DownstreamTarget> { DownstreamTarget.Create("http", "localhost", 5001) },
            "plain");

        var input = RouteConfigurationMapper.Map(route);

        input.AuthenticationOptions.Should().BeNull();
        input.AuthorizationOptions.Should().BeNull();
        input.RateLimitOptions.Should().BeNull();
        input.QoSOptions.Should().BeNull();
        input.CacheOptions.Should().BeNull();
        input.LoadBalancerOptions.Should().BeNull();
        input.HeaderOptions.Should().BeNull();
        input.ClaimOptions.Should().BeNull();
        input.QueryOptions.Should().BeNull();
    }

    [Fact]
    public void Map_ShouldNotShareTheTargetsListWithTheAggregate()
    {
        // A projection that handed back the live list would let a builder
        // change the route, and the "read" would no longer be a read.
        var route = SampleRoute();

        var input = RouteConfigurationMapper.Map(route);
        input.DownstreamTargets.Should().NotBeSameAs(route.DownstreamTargets);
    }
}
