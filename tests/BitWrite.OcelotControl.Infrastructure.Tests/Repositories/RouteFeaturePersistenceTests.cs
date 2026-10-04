using BitWrite.OcelotControl.Domain.Aggregates.Route;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.FeatureConfig;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Infrastructure.Repositories;
using FluentAssertions;
using Moq;
using StackExchange.Redis;
using Xunit;
using HttpMethod = BitWrite.OcelotControl.Domain.ValueObjects.Configuration.HttpMethod;

using BitWrite.OcelotControl.Infrastructure.Tests.Repositories;

namespace BitWrite.OcelotControl.Infrastructure.Tests.Repositories;

/// <summary>
/// Every feature config has to survive a round trip through storage.
/// </summary>
/// <remarks>
/// None of them used to. The create response was built from the in-memory
/// aggregate, so it looked correct, and the options were simply absent on the
/// next read — which also meant a published snapshot dropped them. See #476.
/// </remarks>
public class RouteFeaturePersistenceTests
{
    private readonly Mock<IDatabase> _db = new();
    private readonly Dictionary<string, HashEntry[]> _store = new(StringComparer.Ordinal);

    public RouteFeaturePersistenceTests()
    {
        _db.Setup(d => d.HashSetAsync(
                It.IsAny<RedisKey>(), It.IsAny<HashEntry[]>(), It.IsAny<CommandFlags>()))
            .Returns<RedisKey, HashEntry[], CommandFlags>((key, entries, _) =>
            {
                _store[key.ToString()!] = entries;
                return Task.CompletedTask;
            });

        _db.Setup(d => d.HashGetAllAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .Returns<RedisKey, CommandFlags>((key, _) =>
                Task.FromResult(_store.TryGetValue(key.ToString()!, out var entries)
                    ? entries
                    : Array.Empty<HashEntry>()));
    }

    private RedisRouteRepository NewRoutes()
    {
        var mux = new Mock<IConnectionMultiplexer>();
        mux.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_db.Object);
        return new RedisRouteRepository(mux.Object, TestEnvironment.Context());
    }

    private async Task<Route> RoundTrip(Route route)
    {
        var repository = NewRoutes();
        await repository.AddAsync(route);
        var loaded = await repository.GetAsync(route.Id);
        loaded.Should().NotBeNull();
        return loaded!;
    }

    private static Route NewRoute()
    {
        var route = Route.Create(
            HttpMethod.Get,
            UpstreamPath.From("/api/test"),
            ServiceId.New(),
            new List<DownstreamTarget> { DownstreamTarget.Create("http", "localhost", 5001) },
            "key",
            "api.example.com");
        return route;
    }

    [Fact]
    public async Task Route_ShouldRoundTripAuthentication()
    {
        var route = NewRoute();
        route.SetAuthentication(AuthenticationOptions.Create(
            "Bearer",
            "local",
            new Dictionary<string, string> { ["scopes"] = "users.read,users.write" }));

        var loaded = await RoundTrip(route);

        loaded.AuthenticationOptions.Should().NotBeNull();
        loaded.AuthenticationOptions!.Scheme.Should().Be("Bearer");
        loaded.AuthenticationOptions.Provider.Should().Be("local");
        loaded.AuthenticationOptions.Properties["scopes"].Should().Be("users.read,users.write");
    }

    [Fact]
    public async Task Route_ShouldRoundTripAuthorization()
    {
        var route = NewRoute();
        route.SetAuthorization(AuthorizationOptions.Create(
            new List<string> { "RequireAdmin" },
            new List<string> { "admin" },
            new Dictionary<string, string> { ["role"] = "admin" }));

        var loaded = await RoundTrip(route);

        loaded.AuthorizationOptions.Should().NotBeNull();
        loaded.AuthorizationOptions!.Policies.Should().ContainSingle("RequireAdmin");
        loaded.AuthorizationOptions.Scopes.Should().ContainSingle("admin");
        loaded.AuthorizationOptions.Requirements["role"].Should().Be("admin");
    }

    [Fact]
    public async Task Route_ShouldRoundTripRateLimit()
    {
        var route = NewRoute();
        route.SetRateLimit(RateLimitOptions.Create(250, "Hour", "X-Client", new List<string> { "1.2.3.4" }));

        var loaded = await RoundTrip(route);

        loaded.RateLimitOptions.Should().NotBeNull();
        loaded.RateLimitOptions!.Limit.Should().Be(250);
        loaded.RateLimitOptions.Period.Should().Be("Hour");
        loaded.RateLimitOptions.ClientIdHeader.Should().Be("X-Client");
        loaded.RateLimitOptions.Whitelist.Should().ContainSingle("1.2.3.4");
    }

    [Fact]
    public async Task Route_ShouldRoundTripQoS()
    {
        var route = NewRoute();
        route.SetQoS(QoSOptions.Create(45, 2, true, 10, 3));

        var loaded = await RoundTrip(route);

        loaded.QoSOptions.Should().NotBeNull();
        loaded.QoSOptions!.TimeoutSeconds.Should().Be(45);
        loaded.QoSOptions.RetryCount.Should().Be(2);
        loaded.QoSOptions.UseCircuitBreaker.Should().BeTrue();
        loaded.QoSOptions.CircuitBreakerTimeoutSeconds.Should().Be(10);
        loaded.QoSOptions.CircuitBreakerExceptionsAllowedBeforeBreaking.Should().Be(3);
    }

    [Fact]
    public async Task Route_ShouldRoundTripCache()
    {
        var route = NewRoute();
        route.SetCache(CacheOptions.Create(300, "mykey", "region", new List<string> { "Accept" }));

        var loaded = await RoundTrip(route);

        loaded.CacheOptions.Should().NotBeNull();
        loaded.CacheOptions!.TtlSeconds.Should().Be(300);
        loaded.CacheOptions.Key.Should().Be("mykey");
        loaded.CacheOptions.Region.Should().Be("region");
        loaded.CacheOptions.HeaderNames.Should().ContainSingle("Accept");
    }

    [Fact]
    public async Task Route_ShouldRoundTripLoadBalancer()
    {
        var route = NewRoute();
        route.SetLoadBalancer(LoadBalancerOptions.Create("LeastConnection", "lb"));

        var loaded = await RoundTrip(route);

        loaded.LoadBalancerOptions.Should().NotBeNull();
        loaded.LoadBalancerOptions!.Algorithm.Should().Be("LeastConnection");
        loaded.LoadBalancerOptions.Key.Should().Be("lb");
    }

    [Fact]
    public async Task Route_ShouldRoundTripHeaderTransformations()
    {
        var route = NewRoute();
        route.SetHeaders(HeaderOptions.Create(
            new List<HeaderTransform> { HeaderTransform.Create("X-Api-Key", "secret") },
            new List<string> { "X-Debug" },
            new List<HeaderTransform> { HeaderTransform.Create("X-Trace", "$1") }));

        var loaded = await RoundTrip(route);

        loaded.HeaderOptions.Should().NotBeNull();
        loaded.HeaderOptions!.Add.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { Key = "X-Api-Key", Value = "secret" });
        loaded.HeaderOptions.Remove.Should().ContainSingle("X-Debug");
        loaded.HeaderOptions.Transform.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { Key = "X-Trace", Value = "$1" });
    }

    [Fact]
    public async Task Route_ShouldRoundTripClaimTransformations()
    {
        var route = NewRoute();
        route.SetClaims(ClaimOptions.Create(
            new List<ClaimTransform> { ClaimTransform.Create("sub", "user-1") },
            new List<string> { "stale" },
            null));

        var loaded = await RoundTrip(route);

        loaded.ClaimOptions.Should().NotBeNull();
        loaded.ClaimOptions!.Add.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { Key = "sub", Value = "user-1" });
        loaded.ClaimOptions.Remove.Should().ContainSingle("stale");
    }

    [Fact]
    public async Task Route_ShouldRoundTripQueryTransformations()
    {
        var route = NewRoute();
        route.SetQuery(QueryOptions.Create(
            null,
            new List<string> { "debug" },
            new List<QueryTransform> { QueryTransform.Create("apiKey", "$1") }));

        var loaded = await RoundTrip(route);

        loaded.QueryOptions.Should().NotBeNull();
        loaded.QueryOptions!.Remove.Should().ContainSingle("debug");
        loaded.QueryOptions.Transform.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { Key = "apiKey", Value = "$1" });
    }

    [Fact]
    public async Task Route_ShouldRoundTripPriorityAndCaseSensitivity()
    {
        // These were never written at all, so a reload lost them. Silent again,
        // because a route without a priority still loads and still routes.
        var route = NewRoute();
        route.Replace(
            HttpMethod.Get,
            UpstreamPath.From("/api/test"),
            route.ServiceId,
            new List<DownstreamTarget> { DownstreamTarget.Create("http", "localhost", 5001) },
            "key",
            host: null,
            authenticationOptions: null,
            rateLimitOptions: null,
            qosOptions: null,
            cacheOptions: null,
            loadBalancerOptions: null,
            priority: 250,
            routeIsCaseSensitive: true);

        var loaded = await RoundTrip(route);

        loaded.Priority.Should().Be(250);
        loaded.RouteIsCaseSensitive.Should().BeTrue();
    }

    [Fact]
    public async Task Route_ShouldRoundTripTheUpstreamHost()
    {
        var route = NewRoute();
        route.Replace(
            HttpMethod.Get,
            UpstreamPath.From("/api/test"),
            route.ServiceId,
            new List<DownstreamTarget> { DownstreamTarget.Create("http", "localhost", 5001) },
            "key",
            host: "api.example.com",
            authenticationOptions: null,
            rateLimitOptions: null,
            qosOptions: null,
            cacheOptions: null,
            loadBalancerOptions: null);

        var loaded = await RoundTrip(route);

        loaded.Host.Should().Be("api.example.com");
    }

    [Fact]
    public async Task Route_ShouldReturnNullForOptionsThatWereNeverSet()
    {
        var loaded = await RoundTrip(NewRoute());

        loaded.AuthenticationOptions.Should().BeNull();
        loaded.AuthorizationOptions.Should().BeNull();
        loaded.RateLimitOptions.Should().BeNull();
        loaded.QoSOptions.Should().BeNull();
        loaded.CacheOptions.Should().BeNull();
        loaded.LoadBalancerOptions.Should().BeNull();
        loaded.HeaderOptions.Should().BeNull();
        loaded.ClaimOptions.Should().BeNull();
        loaded.QueryOptions.Should().BeNull();
    }

    [Fact]
    public async Task Route_ShouldStillLoadWhenAnOptionIsAbsent()
    {
        // Rows written before these fields existed have nothing stored, and the
        // read must not fail on them — a throw here empties the whole route list,
        // which is how a bad read turned into "no routes" before.
        var repository = NewRoutes();
        var route = NewRoute();
        await repository.AddAsync(route);

        // Simulate a row written before the option fields existed. The store is
        // keyed by the prefixed Redis key, so find it by suffix.
        var key = _store.Keys.Single(k => k.EndsWith(route.Id.Value.ToString(), StringComparison.Ordinal));
        _store[key] = _store[key]
            .Where(entry => entry.Name != "RateLimitOptions")
            .ToArray();

        var loaded = await repository.GetAsync(route.Id);

        loaded.Should().NotBeNull();
        loaded!.RateLimitOptions.Should().BeNull();
    }

    [Fact]
    public async Task Route_ShouldReadBackEverythingItWasGiven()
    {
        // The whole point: what was set is what a later read reports.
        var route = NewRoute();
        route.SetAuthentication(AuthenticationOptions.Create("Bearer"));
        route.SetAuthorization(AuthorizationOptions.Create(new List<string> { "P" }));
        route.SetRateLimit(RateLimitOptions.Create(10, "Minute"));
        route.SetQoS(QoSOptions.Create(30));
        route.SetCache(CacheOptions.Create(60));
        route.SetLoadBalancer(LoadBalancerOptions.RoundRobin());
        route.SetHeaders(HeaderOptions.Create(null, new List<string> { "X-Drop" }, null));
        route.SetClaims(ClaimOptions.Create(null, new List<string> { "old" }, null));
        route.SetQuery(QueryOptions.Create(null, new List<string> { "trace" }, null));

        var loaded = await RoundTrip(route);

        loaded.AuthenticationOptions.Should().NotBeNull();
        loaded.AuthorizationOptions.Should().NotBeNull();
        loaded.RateLimitOptions.Should().NotBeNull();
        loaded.QoSOptions.Should().NotBeNull();
        loaded.CacheOptions.Should().NotBeNull();
        loaded.LoadBalancerOptions.Should().NotBeNull();
        loaded.HeaderOptions.Should().NotBeNull();
        loaded.ClaimOptions.Should().NotBeNull();
        loaded.QueryOptions.Should().NotBeNull();
    }

    [Fact]
    public async Task Route_ShouldRoundTripTransportSettings()
    {
        var route = NewRoute();
        route.SetDownstreamMethod(HttpMethod.Post);
        route.SetDownstreamHttpVersion("2.0", "RequestVersionExact");
        route.SetAcceptAnyServerCertificate(true);
        route.SetDelegatingHandlers(new[] { "First", "Second" });
        route.SetHttpClientOptions(HttpClientOptions.Create(
            allowAutoRedirect: true,
            maxConnectionsPerServer: 25,
            pooledConnectionLifetimeSeconds: 400,
            useCookieContainer: true,
            useProxy: true,
            useTracing: true));
        route.SetTimeout(75);

        var loaded = await RoundTrip(route);

        loaded.DownstreamMethod!.Value.Should().Be("POST");
        loaded.DownstreamHttpVersion.Should().Be("2.0");
        loaded.DownstreamHttpVersionPolicy.Should().Be("RequestVersionExact");
        loaded.DangerousAcceptAnyServerCertificateValidator.Should().BeTrue();
        loaded.DelegatingHandlers.Should().BeEquivalentTo(new[] { "First", "Second" });
        loaded.TimeoutSeconds.Should().Be(75);
        loaded.HttpClientOptions.Should().NotBeNull();
        loaded.HttpClientOptions!.AllowAutoRedirect.Should().BeTrue();
        loaded.HttpClientOptions.MaxConnectionsPerServer.Should().Be(25);
        loaded.HttpClientOptions.PooledConnectionLifetimeSeconds.Should().Be(400);
        loaded.HttpClientOptions.UseCookieContainer.Should().BeTrue();
        loaded.HttpClientOptions.UseProxy.Should().BeTrue();
        loaded.HttpClientOptions.UseTracing.Should().BeTrue();
    }

    [Fact]
    public async Task Route_WithoutTransportSettings_ShouldLoadWithDefaults()
    {
        var loaded = await RoundTrip(NewRoute());

        loaded.DownstreamMethod.Should().BeNull();
        loaded.DownstreamHttpVersion.Should().BeNull();
        loaded.DownstreamHttpVersionPolicy.Should().BeNull();
        loaded.DangerousAcceptAnyServerCertificateValidator.Should().BeFalse();
        loaded.DelegatingHandlers.Should().BeEmpty();
        loaded.HttpClientOptions.Should().BeNull();
        loaded.TimeoutSeconds.Should().BeNull();
    }

    [Fact]
    public async Task Route_ShouldNotStoreEmptyStringsAsSetValues()
    {
        // An empty stored entry means "not set". Storing "" for a null value
        // would come back as an empty version rather than absent.
        var loaded = await RoundTrip(NewRoute());

        var entries = _store.Should().ContainSingle().Subject.Value;
        entries.Should().Contain(e => e.Name == "DownstreamMethod" && e.Value!.ToString() == "");
        entries.Should().Contain(e => e.Name == "DownstreamHttpVersion" && e.Value!.ToString() == "");
        entries.Should().Contain(e => e.Name == "DownstreamHttpVersionPolicy" && e.Value!.ToString() == "");
        entries.Should().Contain(e => e.Name == "HttpClientOptions" && e.Value!.ToString() == "");
        entries.Should().Contain(e => e.Name == "TimeoutSeconds" && e.Value!.ToString() == "");
    }

    [Fact]
    public async Task Route_ShouldRoundTripTheDownstreamPathTemplate()
    {
        var route = NewRoute();
        route.Replace(
            route.Method,
            UpstreamPath.From("/api/orders/{orderId}"),
            route.ServiceId,
            new List<DownstreamTarget> { DownstreamTarget.Create("https", "orders.internal.example.com", 443) },
            key: null,
            host: null,
            authenticationOptions: null,
            rateLimitOptions: null,
            qosOptions: null,
            cacheOptions: null,
            loadBalancerOptions: null,
            downstreamTemplate: DownstreamPathTemplate.From("/internal/orders/{orderId}"));

        var loaded = await RoundTrip(route);

        loaded.UpstreamPath.Value.Should().Be("/api/orders/{orderId}");
        loaded.DownstreamTemplate.Should().NotBeNull();
        loaded.DownstreamTemplate!.Value.Should().Be("/internal/orders/{orderId}");
    }

    [Fact]
    public async Task Route_WithoutADownstreamTemplate_ShouldLoadWithNothing()
    {
        // Absent, not an empty string: an empty template would not parse.
        var loaded = await RoundTrip(NewRoute());

        loaded.DownstreamTemplate.Should().BeNull();

        var entries = _store.Should().ContainSingle().Subject.Value;
        entries.Should().Contain(e => e.Name == "DownstreamTemplate" && e.Value!.ToString() == "");
    }
}
