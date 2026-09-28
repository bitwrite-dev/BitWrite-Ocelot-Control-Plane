using BitWrite.OcelotControl.Domain.Aggregates.Route;
using BitWrite.OcelotControl.Domain.Services;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.FeatureConfig;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using FluentAssertions;
using Xunit;
using HttpMethod = BitWrite.OcelotControl.Domain.ValueObjects.Configuration.HttpMethod;
using MinimalGlobalConfig = BitWrite.OcelotControl.Domain.Services.GlobalConfiguration;

namespace BitWrite.OcelotControl.Domain.Tests.Services;

/// <summary>
/// The generated Ocelot configuration, and the projection that feeds it.
/// </summary>
public class RouteConfigurationEmissionTests
{
    private static Route SampleRoute()
    {
        var route = Route.Create(
            HttpMethod.Get,
            UpstreamPath.From("/api/users"),
            ServiceId.New(),
            new List<DownstreamTarget> { DownstreamTarget.Create("http", "localhost", 5001) },
            "users-list");

        route.SetAuthentication(AuthenticationOptions.Create(
            "Bearer",
            null,
            new Dictionary<string, string> { ["scopes"] = "users.read, users.write" }));

        return route;
    }

    private static RouteConfiguration BuilderInput(Route route) =>
        new()
        {
            Id = route.Id,
            Host = route.Host,
            Method = route.Method,
            UpstreamPath = route.UpstreamPath,
            ServiceId = route.ServiceId,
            DownstreamTargets = route.DownstreamTargets
                .Select(t => DownstreamTarget.Create(t.Scheme, t.Host, t.Port, t.Path))
                .ToList(),
            AuthenticationOptions = route.AuthenticationOptions,
            AuthorizationOptions = route.AuthorizationOptions,
            RateLimitOptions = route.RateLimitOptions,
            QoSOptions = route.QoSOptions,
            CacheOptions = route.CacheOptions,
            LoadBalancerOptions = route.LoadBalancerOptions,
            HeaderOptions = route.HeaderOptions,
            ClaimOptions = route.ClaimOptions,
            QueryOptions = route.QueryOptions
        };

    private static OcelotConfiguration Build(Route route)
    {
        var builder = new ConfigurationBuilder(new ConfigurationCanonicalizer());
        return builder.BuildConfiguration(
            new List<RouteConfiguration> { BuilderInput(route) },
            new MinimalGlobalConfig { BaseUrl = "", RequestIdKey = "" },
            // 20 is not emittable yet, and the builder now says so rather than
            // quietly emitting 18's shapes for it. See #484.
            OcelotVersion.V18_0);
    }

    [Fact]
    public void BuildConfiguration_ShouldPublishTheConfiguredScopes()
    {
        // These used to be published as an empty list, so authentication reached
        // the gateway with no scopes and was ignored.
        var config = Build(SampleRoute());

        var route = config.Routes.Should().ContainSingle().Subject;
        route.AuthenticationOptions.Should().NotBeNull();
        route.AuthenticationOptions!.AllowedScopes.Should().BeEquivalentTo(
            new[] { "users.read", "users.write" });
    }

    [Fact]
    public void BuildConfiguration_ShouldPublishNoAuthenticationBlockWhenUnset()
    {
        var route = Route.Create(
            HttpMethod.Get,
            UpstreamPath.From("/api/users"),
            ServiceId.New(),
            new List<DownstreamTarget> { DownstreamTarget.Create("http", "localhost", 5001) },
            "users-list");

        var config = Build(route);

        config.Routes.Should().ContainSingle()
            .Which.AuthenticationOptions.Should().BeNull();
    }

    [Fact]
    public void BuildConfiguration_ShouldOmitAnAuthenticationBlockWithNoScopes()
    {
        // An empty block would tell the gateway authentication is configured
        // with nothing to allow, which is different from not configuring it.
        var route = SampleRoute();
        route.SetAuthentication(AuthenticationOptions.Create("Bearer"));

        var config = Build(route);

        config.Routes.Should().ContainSingle()
            .Which.AuthenticationOptions!.AllowedScopes.Should().BeEmpty();
    }
}
