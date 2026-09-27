using BitWrite.OcelotControl.Domain.Aggregates.Route;
using BitWrite.OcelotControl.Domain.Services;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using FluentAssertions;
using Xunit;
using HttpMethod = BitWrite.OcelotControl.Domain.ValueObjects.Configuration.HttpMethod;
using MinimalGlobalConfig = BitWrite.OcelotControl.Domain.Services.GlobalConfiguration;

namespace BitWrite.OcelotControl.Domain.Tests.Services;

/// <summary>
/// Route capabilities that change how a route behaves rather than adding an
/// option: evaluation order, path matching, and the upstream host.
/// </summary>
public class RouteCapabilityEmissionTests
{
    private readonly ConfigurationBuilder _builder = new(new ConfigurationCanonicalizer());

    private static Route SampleRoute() =>
        Route.Create(
            HttpMethod.Get,
            UpstreamPath.From("/api/users"),
            ServiceId.New(),
            new List<DownstreamTarget> { DownstreamTarget.Create("http", "localhost", 5001) },
            "users-list");

    private OcelotRouteConfiguration Emit(
        Route route,
        int priority = 0,
        bool caseSensitive = false,
        string? host = null) =>
        _builder.BuildConfiguration(
            new List<RouteConfiguration>
            {
                new()
                {
                    Id = route.Id,
                    Host = host,
                    Method = route.Method,
                    UpstreamPath = route.UpstreamPath,
                    ServiceId = route.ServiceId,
                    DownstreamTargets = route.DownstreamTargets
                        .Select(t => DownstreamTarget.Create(t.Scheme, t.Host, t.Port, t.Path))
                        .ToList(),
                    Priority = priority,
                    RouteIsCaseSensitive = caseSensitive,
                }
            },
            new MinimalGlobalConfig { BaseUrl = "", RequestIdKey = "" },
            ConfigurationBuilder.BaselineVersion).Routes.Single();

    [Fact]
    public void Priority_ShouldBeEmitted()
    {
        // Without it Ocelot orders overlapping routes by file order, which an
        // operator cannot control or reason about.
        Emit(SampleRoute(), priority: 100).Priority.Should().Be(100);
    }

    [Fact]
    public void Priority_ShouldDefaultToZero()
    {
        Emit(SampleRoute()).Priority.Should().Be(0);
    }

    [Fact]
    public void CaseSensitivity_ShouldBeEmitted()
    {
        Emit(SampleRoute(), caseSensitive: true).RouteIsCaseSensitive.Should().BeTrue();
    }

    [Fact]
    public void CaseSensitivity_ShouldDefaultToInsensitive()
    {
        // The Ocelot default, and the reason /api/Users and /api/users collide.
        Emit(SampleRoute()).RouteIsCaseSensitive.Should().BeFalse();
    }

    [Fact]
    public void UpstreamHost_ShouldBeEmitted()
    {
        // The route has always held a host. It reached neither the configuration
        // nor the API response, so setting it did nothing at all.
        Emit(SampleRoute(), host: "api.example.com").UpstreamHost.Should().Be("api.example.com");
    }

    [Fact]
    public void UpstreamHost_ShouldBeOmittedWhenUnset()
    {
        Emit(SampleRoute()).UpstreamHost.Should().BeNull();
    }

    [Fact]
    public void Replace_ShouldSetPriorityAndCaseSensitivity()
    {
        var route = SampleRoute();

        route.Replace(
            HttpMethod.Get,
            UpstreamPath.From("/api/users"),
            route.ServiceId,
            new List<DownstreamTarget> { DownstreamTarget.Create("http", "localhost", 5001) },
            "users-list",
            null,
            null, null, null, null, null,
            null, null, null,
            priority: 50,
            routeIsCaseSensitive: true);

        route.Priority.Should().Be(50);
        route.RouteIsCaseSensitive.Should().BeTrue();
    }

    [Fact]
    public void Replace_ShouldResetThemWhenNotSupplied()
    {
        // A replacement is a replacement, so an omitted value is not "keep".
        var route = SampleRoute();
        route.Replace(
            HttpMethod.Get, UpstreamPath.From("/api/users"), route.ServiceId,
            new List<DownstreamTarget> { DownstreamTarget.Create("http", "localhost", 5001) },
            "users-list", null, null, null, null, null, null, null, null, null,
            priority: 50, routeIsCaseSensitive: true);

        route.Replace(
            HttpMethod.Get, UpstreamPath.From("/api/users"), route.ServiceId,
            new List<DownstreamTarget> { DownstreamTarget.Create("http", "localhost", 5001) },
            "users-list", null, null, null, null, null, null, null, null, null,
            priority: 0, routeIsCaseSensitive: false);

        route.Priority.Should().Be(0);
        route.RouteIsCaseSensitive.Should().BeFalse();
    }

    [Fact]
    public void Reconstitute_ShouldRestoreThem()
    {
        var id = RouteId.New();
        var created = DateTimeOffset.UtcNow.AddDays(-1);

        var route = Route.Reconstitute(
            id,
            HttpMethod.Get,
            UpstreamPath.From("/api/users"),
            ServiceId.New(),
            new List<DownstreamTarget> { DownstreamTarget.Create("http", "localhost", 5001) },
            isEnabled: true,
            created,
            DateTimeOffset.UtcNow,
            key: "users-list",
            host: "api.example.com",
            priority: 75,
            routeIsCaseSensitive: true);

        route.Priority.Should().Be(75);
        route.RouteIsCaseSensitive.Should().BeTrue();
        route.Host.Should().Be("api.example.com");
    }
}
