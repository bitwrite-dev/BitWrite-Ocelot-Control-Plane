using BitWrite.OcelotControl.Domain.Aggregates.Route;
using BitWrite.OcelotControl.Domain.Exceptions;
using BitWrite.OcelotControl.Domain.Services;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.FeatureConfig;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using Xunit;
using HttpMethod = BitWrite.OcelotControl.Domain.ValueObjects.Configuration.HttpMethod;
using MinimalGlobalConfig = BitWrite.OcelotControl.Domain.Services.GlobalConfiguration;

namespace BitWrite.OcelotControl.Domain.Tests.Services;

/// <summary>
/// Covers the transport and client-behaviour capabilities of #485 second slice.
/// </summary>
public class RouteTransportEmissionTests
{
    private readonly ConfigurationBuilder _builder = new(new ConfigurationCanonicalizer());

    private static Route NewRoute(
        DownstreamPathTemplate? downstreamTemplate = null,
        HttpMethod? downstreamMethod = null,
        string? downstreamHttpVersion = null,
        string? downstreamHttpVersionPolicy = null,
        bool acceptAnyServerCertificate = false,
        IReadOnlyList<string>? delegatingHandlers = null,
        HttpClientOptions? httpClientOptions = null,
        int? timeoutSeconds = null)
    {
        var route = Route.Create(
            HttpMethod.Get,
            UpstreamPath.From("/api/test"),
            ServiceId.New(),
            new List<DownstreamTarget> { DownstreamTarget.Create("http", "localhost", 5001) },
            "test-route");

        route.SetDownstreamMethod(downstreamMethod);
        route.SetDownstreamHttpVersion(downstreamHttpVersion, downstreamHttpVersionPolicy);
        route.SetAcceptAnyServerCertificate(acceptAnyServerCertificate);
        route.SetDelegatingHandlers(delegatingHandlers);
        route.SetHttpClientOptions(httpClientOptions);
        route.SetDownstreamTemplate(downstreamTemplate);
        route.SetTimeout(timeoutSeconds);

        return route;
    }

    private OcelotRouteConfiguration Emit(Route route) =>
        _builder.BuildConfiguration(
            new List<RouteConfiguration>
            {
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
                    QoSOptions = route.QoSOptions,
                    DownstreamTemplate = route.DownstreamTemplate,
                    DownstreamMethod = route.DownstreamMethod,                    DownstreamHttpVersion = route.DownstreamHttpVersion,
                    DownstreamHttpVersionPolicy = route.DownstreamHttpVersionPolicy,
                    DangerousAcceptAnyServerCertificateValidator =
                        route.DangerousAcceptAnyServerCertificateValidator,
                    DelegatingHandlers = route.DelegatingHandlers.ToList(),
                    HttpClientOptions = route.HttpClientOptions,
                    TimeoutSeconds = route.TimeoutSeconds
                }
            },
            new MinimalGlobalConfig { BaseUrl = "", RequestIdKey = "" },
            ConfigurationBuilder.BaselineVersion).Routes.Single();

    [Fact]
    public void DownstreamMethodIsEmittedAsASingleString()
    {
        var emitted = Emit(NewRoute(downstreamMethod: HttpMethod.Post));

        // Ocelot models the downstream verb as one string, not a list like the
        // upstream one, so a list here would not bind.
        Assert.Equal("POST", emitted.DownstreamHttpMethod);
    }

    [Fact]
    public void DownstreamMethodIsOmittedWhenNotSet()
    {
        Assert.Null(Emit(NewRoute()).DownstreamHttpMethod);
    }

    [Theory]
    [InlineData("1.0")]
    [InlineData("1.1")]
    [InlineData("2.0")]
    public void SupportedDownstreamVersionsAreEmitted(string version)
    {
        Assert.Equal(version, Emit(NewRoute(downstreamHttpVersion: version)).DownstreamHttpVersion);
    }

    [Theory]
    [InlineData("3.0")]
    [InlineData("2")]
    [InlineData("1.1.1")]
    [InlineData("latest")]
    public void UnsupportedDownstreamVersionsAreRejected(string version)
    {
        var route = NewRoute();
        var ex = Assert.Throws<DomainException>(() => route.SetDownstreamHttpVersion(version));

        Assert.Equal("INVALID_HTTP_VERSION", ex.ErrorCode);
    }

    [Theory]
    [InlineData("RequestVersionExact")]
    [InlineData("RequestVersionOrHigher")]
    [InlineData("RequestVersionOrLower")]
    public void SupportedVersionPoliciesAreEmitted(string policy)
    {
        Assert.Equal(
            policy,
            Emit(NewRoute(downstreamHttpVersion: "2.0", downstreamHttpVersionPolicy: policy))
                .DownstreamHttpVersionPolicy);
    }

    [Fact]
    public void UnknownVersionPolicyIsRejected()
    {
        var route = NewRoute();
        var ex = Assert.Throws<DomainException>(
            () => route.SetDownstreamHttpVersion("2.0", "RequestVersionMaybe"));

        Assert.Equal("INVALID_HTTP_VERSION_POLICY", ex.ErrorCode);
    }

    [Fact]
    public void PolicyWithoutAVersionIsRejected()
    {
        var route = NewRoute();
        var ex = Assert.Throws<DomainException>(
            () => route.SetDownstreamHttpVersion(null, "RequestVersionExact"));

        Assert.Equal("HTTP_VERSION_POLICY_WITHOUT_VERSION", ex.ErrorCode);
    }

    [Fact]
    public void AcceptAnyServerCertificateIsOnlyEmittedWhenEnabled()
    {
        Assert.False(Emit(NewRoute(acceptAnyServerCertificate: false))
            .DangerousAcceptAnyServerCertificateValidator);

        Assert.True(Emit(NewRoute(acceptAnyServerCertificate: true))
            .DangerousAcceptAnyServerCertificateValidator);
    }

    [Fact]
    public void DelegatingHandlersAreEmittedInOrder()
    {
        Assert.Equal(new[] { "First", "Second" }, Emit(NewRoute(delegatingHandlers: new[] { "First", "Second" })).DelegatingHandlers);
    }

    [Fact]
    public void EmptyDelegatingHandlersAreOmitted()
    {
        Assert.Null(Emit(NewRoute(delegatingHandlers: Array.Empty<string>())).DelegatingHandlers);
    }

    [Fact]
    public void BlankDelegatingHandlerIsRejected()
    {
        var route = NewRoute();
        var ex = Assert.Throws<DomainException>(
            () => route.SetDelegatingHandlers(new[] { "First", "  " }));

        Assert.Equal("INVALID_DELEGATING_HANDLER", ex.ErrorCode);
    }

    [Fact]
    public void DuplicateDelegatingHandlerIsRejected()
    {
        // Ocelot would register the same handler twice.
        var route = NewRoute();
        var ex = Assert.Throws<DomainException>(
            () => route.SetDelegatingHandlers(new[] { "Handler", "handler" }));

        Assert.Equal("DUPLICATE_DELEGATING_HANDLER", ex.ErrorCode);
    }

    [Fact]
    public void HttpHandlerOptionsAreEmitted()
    {
        var emitted = Emit(NewRoute(httpClientOptions: HttpClientOptions.Create(
            allowAutoRedirect: true,
            maxConnectionsPerServer: 20,
            pooledConnectionLifetimeSeconds: 300,
            useCookieContainer: true,
            useTracing: true))).HttpHandlerOptions!;

        Assert.True(emitted.AllowAutoRedirect);
        Assert.Equal(20, emitted.MaxConnectionsPerServer);
        Assert.Equal(300, emitted.PooledConnectionLifetime);
        Assert.True(emitted.UseCookieContainer);
        Assert.True(emitted.UseTracing);
    }

    [Fact]
    public void HttpHandlerOptionsAreOmittedWhenNotSet()
    {
        Assert.Null(Emit(NewRoute()).HttpHandlerOptions);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveTimeoutIsRejected(int seconds)
    {
        // Ocelot reads zero or less as "no timeout", which is a quiet way to
        // wait forever.
        var route = NewRoute();
        var ex = Assert.Throws<DomainException>(() => route.SetTimeout(seconds));

        Assert.Equal("INVALID_TIMEOUT", ex.ErrorCode);
    }

    [Fact]
    public void TimeoutIsEmittedSeparatelyFromQoS()
    {
        var route = NewRoute(timeoutSeconds: 45);
        route.SetQoS(QoSOptions.Create(timeoutSeconds: 5, circuitBreakerTimeoutSeconds: 10));

        var emitted = Emit(route);

        // Two different settings: the route timeout bounds the whole call, the
        // QoS one is the retry policy. They must not overwrite each other.
        Assert.Equal(45, emitted.Timeout);
        Assert.Equal(5, emitted.QoSOptions!.TimeoutValue);
    }

    [Fact]
    public void TransportSettingsSurviveReconstitute()
    {
        var original = NewRoute(
            downstreamMethod: HttpMethod.Post,
            downstreamHttpVersion: "2.0",
            downstreamHttpVersionPolicy: "RequestVersionExact",
            acceptAnyServerCertificate: true,
            delegatingHandlers: new[] { "Handler" },
            httpClientOptions: HttpClientOptions.Create(allowAutoRedirect: true, maxConnectionsPerServer: 12),
            timeoutSeconds: 90);

        var restored = Route.Reconstitute(
            original.Id,
            original.Method,
            original.UpstreamPath,
            original.ServiceId,
            original.DownstreamTargets,
            original.IsEnabled,
            original.CreatedAt,
            original.UpdatedAt,
            priority: original.Priority,
            routeIsCaseSensitive: original.RouteIsCaseSensitive,
            downstreamMethod: original.DownstreamMethod,
            downstreamHttpVersion: original.DownstreamHttpVersion,
            downstreamHttpVersionPolicy: original.DownstreamHttpVersionPolicy,
            acceptAnyServerCertificate: original.DangerousAcceptAnyServerCertificateValidator,
            delegatingHandlers: original.DelegatingHandlers,
            httpClientOptions: original.HttpClientOptions,
            timeoutSeconds: original.TimeoutSeconds,
            authenticationOptions: original.AuthenticationOptions,
            authorizationOptions: original.AuthorizationOptions,
            rateLimitOptions: original.RateLimitOptions,
            qosOptions: original.QoSOptions,
            cacheOptions: original.CacheOptions,
            loadBalancerOptions: original.LoadBalancerOptions,
            headerOptions: original.HeaderOptions,
            claimOptions: original.ClaimOptions,
            queryOptions: original.QueryOptions);

        Assert.Equal(HttpMethod.Post, restored.DownstreamMethod);
        Assert.Equal("2.0", restored.DownstreamHttpVersion);
        Assert.Equal("RequestVersionExact", restored.DownstreamHttpVersionPolicy);
        Assert.True(restored.DangerousAcceptAnyServerCertificateValidator);
        Assert.Equal(new[] { "Handler" }, restored.DelegatingHandlers);
        Assert.Equal(90, restored.TimeoutSeconds);
        Assert.Equal(12, restored.HttpClientOptions!.MaxConnectionsPerServer);
    }

    [Fact]
    public void TransportSettingsSurviveReplace()
    {
        var route = NewRoute(
            downstreamMethod: HttpMethod.Post,
            downstreamHttpVersion: "2.0",
            delegatingHandlers: new[] { "Handler" },
            timeoutSeconds: 60);

        route.Replace(
            route.Method,
            route.UpstreamPath,
            route.ServiceId,
            route.DownstreamTargets,
            key: null,
            host: null,
            authenticationOptions: null,
            rateLimitOptions: null,
            qosOptions: null,
            cacheOptions: null,
            loadBalancerOptions: null,
            downstreamMethod: HttpMethod.Get,
            downstreamHttpVersion: "1.1",
            downstreamHttpVersionPolicy: "RequestVersionOrHigher",
            acceptAnyServerCertificate: true,
            delegatingHandlers: new[] { "Other" },
            httpClientOptions: HttpClientOptions.Create(useTracing: true),
            timeoutSeconds: 30);

        // A replacement is a full one, so nothing may survive from the old state.
        Assert.Equal(HttpMethod.Get, route.DownstreamMethod);
        Assert.Equal("1.1", route.DownstreamHttpVersion);
        Assert.Equal("RequestVersionOrHigher", route.DownstreamHttpVersionPolicy);
        Assert.True(route.DangerousAcceptAnyServerCertificateValidator);
        Assert.Equal(new[] { "Other" }, route.DelegatingHandlers);
        Assert.Equal(30, route.TimeoutSeconds);
        Assert.True(route.HttpClientOptions!.UseTracing);
    }

    [Fact]
    public void ReplaceWithoutTransportSettingsClearsThem()
    {
        var route = NewRoute(
            downstreamMethod: HttpMethod.Post,
            downstreamHttpVersion: "2.0",
            delegatingHandlers: new[] { "Handler" },
            timeoutSeconds: 60);

        route.Replace(
            route.Method,
            route.UpstreamPath,
            route.ServiceId,
            route.DownstreamTargets,
            key: null,
            host: null,
            authenticationOptions: null,
            rateLimitOptions: null,
            qosOptions: null,
            cacheOptions: null,
            loadBalancerOptions: null);

        Assert.Null(route.DownstreamMethod);
        Assert.Null(route.DownstreamHttpVersion);
        Assert.Empty(route.DelegatingHandlers);
        Assert.Null(route.TimeoutSeconds);
    }

    [Fact]
    public void DownstreamPathTemplateIsEmittedWhenSet()
    {
        // Was hardcoded to "/{everything}", so a service under a different
        // prefix could not be reached at all.
        var route = NewRoute();
        route.Replace(
            route.Method,
            UpstreamPath.From("/api/orders/{orderId}"),
            route.ServiceId,
            route.DownstreamTargets,
            key: null,
            host: null,
            authenticationOptions: null,
            rateLimitOptions: null,
            qosOptions: null,
            cacheOptions: null,
            loadBalancerOptions: null,
            downstreamTemplate: DownstreamPathTemplate.From("/internal/orders/{orderId}"));

        Assert.Equal("/internal/orders/{orderId}", Emit(route).DownstreamPathTemplate);
    }

    [Fact]
    public void DownstreamPathTemplateDefaultsToEverything()
    {
        // Ocelot's catch-all: the upstream path is forwarded unchanged.
        Assert.Equal("/{everything}", Emit(NewRoute()).DownstreamPathTemplate);
    }

    [Fact]
    public void APlaceholderTheUpstreamPathCannotFillIsRejected()
    {
        // It would arrive empty at the service, which reads as a puzzling 404.
        var route = NewRoute();
        var ex = Assert.Throws<DomainException>(
            () => route.SetDownstreamTemplate(DownstreamPathTemplate.From("/internal/{unknown}")));

        Assert.Equal("DOWNSTREAM_PLACEHOLDER_NOT_IN_UPSTREAM", ex.ErrorCode);
    }

    [Fact]
    public void TheEverythingPlaceholderIsAlwaysAvailable()
    {
        // Ocelot fills it whatever the upstream path looks like.
        var route = NewRoute();
        route.Replace(
            route.Method,
            UpstreamPath.From("/api/users"),
            route.ServiceId,
            route.DownstreamTargets,
            key: null,
            host: null,
            authenticationOptions: null,
            rateLimitOptions: null,
            qosOptions: null,
            cacheOptions: null,
            loadBalancerOptions: null,
            downstreamTemplate: DownstreamPathTemplate.From("/internal/{everything}"));

        Assert.Equal("/internal/{everything}", route.DownstreamTemplate!.Value);
    }

    [Fact]
    public void DownstreamPathTemplateSurvivesReconstitute()
    {
        var original = NewRoute(downstreamTemplate: DownstreamPathTemplate.From("/internal/{everything}"));

        var restored = Route.Reconstitute(
            original.Id,
            original.Method,
            original.UpstreamPath,
            original.ServiceId,
            original.DownstreamTargets,
            original.IsEnabled,
            original.CreatedAt,
            original.UpdatedAt,
            downstreamTemplate: original.DownstreamTemplate);

        Assert.Equal("/internal/{everything}", restored.DownstreamTemplate!.Value);
    }

    [Theory]
    [InlineData("/api/{}")]
    [InlineData("/api/{unclosed")]
    [InlineData("/api/{has space}")]
    public void AMalformedPlaceholderIsRejected(string template)
    {
        Assert.Throws<DomainException>(() => DownstreamPathTemplate.From(template));
    }

    [Fact]
    public void TheOperatorsKeyIsPublishedRatherThanTheComputedSignature()
    {
        // The signature was written here, so a route saved as
        // "orders-rewrite" reached the gateway as "GET:/api/orders" and lost
        // the name the operator gave it.
        var route = NewRoute();
        route.Replace(
            route.Method,
            UpstreamPath.From("/api/orders/{orderId}"),
            route.ServiceId,
            new List<DownstreamTarget> { DownstreamTarget.Create("https", "orders.internal.example.com", 443) },
            key: "orders-rewrite",
            host: null,
            authenticationOptions: null,
            rateLimitOptions: null,
            qosOptions: null,
            cacheOptions: null,
            loadBalancerOptions: null);

        var emitted = _builder.BuildConfiguration(
            new List<RouteConfiguration>
            {
                new()
                {
                    Id = route.Id,
                    Host = route.Host,
                    FriendlyKey = route.Key,
                    Method = route.Method,
                    UpstreamPath = route.UpstreamPath,
                    ServiceId = route.ServiceId,
                    DownstreamTargets = route.DownstreamTargets,
                }
            },
            new MinimalGlobalConfig { BaseUrl = "", RequestIdKey = "" },
            ConfigurationBuilder.BaselineVersion).Routes.Single();

        Assert.Equal("orders-rewrite", emitted.Key);
    }

    [Fact]
    public void NoKeyMeansNoKeyRatherThanAComputedSignature()
    {
        // Ocelot treats an absent Key as fine, so a null is emitted as nothing.
        var route = NewRoute();
        route.Replace(
            route.Method,
            UpstreamPath.From("/api/orders"),
            route.ServiceId,
            new List<DownstreamTarget> { DownstreamTarget.Create("http", "localhost", 5001) },
            key: null,
            host: null,
            authenticationOptions: null,
            rateLimitOptions: null,
            qosOptions: null,
            cacheOptions: null,
            loadBalancerOptions: null);

        var emitted = _builder.BuildConfiguration(
            new List<RouteConfiguration>
            {
                new()
                {
                    Id = route.Id,
                    Host = route.Host,
                    FriendlyKey = route.Key,
                    Method = route.Method,
                    UpstreamPath = route.UpstreamPath,
                    ServiceId = route.ServiceId,
                    DownstreamTargets = route.DownstreamTargets,
                }
            },
            new MinimalGlobalConfig { BaseUrl = "", RequestIdKey = "" },
            ConfigurationBuilder.BaselineVersion).Routes.Single();

        Assert.Null(emitted.Key);
    }
}
