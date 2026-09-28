using System.ComponentModel.DataAnnotations;
using BitWrite.OcelotControl.Api.DTOs;
using BitWrite.OcelotControl.Api.Mapping;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using FluentAssertions;
using Xunit;

namespace BitWrite.OcelotControl.Api.Tests.Mapping;

/// <summary>
/// The request contract for the transport and client-behaviour capabilities of
/// #485 second slice.
/// </summary>
/// <remarks>
/// Each of these changes what the gateway does with the request. Anything the
/// API silently dropped was a setting an operator entered and then watched not
/// take effect.
/// </remarks>
public class RouteRequestMapperTransportTests
{
    private static CreateRouteRequest Request(
        string? downstreamMethod = null,
        string? downstreamPathTemplate = null,
        string? downstreamHttpVersion = null,
        string? downstreamHttpVersionPolicy = null,
        bool acceptAnyServerCertificate = false,
        List<string>? delegatingHandlers = null,
        HttpClientOptionsRequest? httpClientOptions = null,
        int? timeoutSeconds = null) =>
        new(
            Key: "users-list",
            Method: "GET",
            UpstreamPath: "/api/users",
            Host: null,
            ServiceId: Guid.NewGuid().ToString(),
            DownstreamTargets: new List<DownstreamTargetRequest> { new("localhost", 5001, "http", "/") },
            AuthenticationOptions: null,
            AuthorizationOptions: null,
            RateLimitOptions: null,
            QoSOptions: null,
            CacheOptions: null,
            LoadBalancerOptions: null,
            HeaderTransformations: null,
            ClaimTransformations: null,
            QueryTransformations: null,
            DownstreamMethod: downstreamMethod,
            DownstreamPathTemplate: downstreamPathTemplate,
            DownstreamHttpVersion: downstreamHttpVersion,
            DownstreamHttpVersionPolicy: downstreamHttpVersionPolicy,
            AcceptAnyServerCertificate: acceptAnyServerCertificate,
            DelegatingHandlers: delegatingHandlers,
            HttpClientOptions: httpClientOptions,
            TimeoutSeconds: timeoutSeconds);

    [Fact]
    public void ToCreateCommand_ShouldCarryTheDownstreamMethod()
    {
        var mapping = RouteRequestMapper.ToCreateCommand(Request(downstreamMethod: "POST"), "tester");

        mapping.Success.Should().BeTrue("the mapping failed: " + string.Join(", ", mapping.Errors.Select(e => e.Field)));
        mapping.Value!.DownstreamMethod!.Value.Should().Be("POST");
    }

    [Fact]
    public void ToCreateCommand_ShouldLeaveTheDownstreamMethodNullWhenAbsent()
    {
        var mapping = RouteRequestMapper.ToCreateCommand(Request(), "tester");

        mapping.Value!.DownstreamMethod.Should().BeNull();
    }

    [Fact]
    public void ToCreateCommand_ShouldRejectAnUnknownDownstreamMethod()
    {
        // Ocelot would not bind it, so the request fails here rather than
        // reaching the gateway as a no-op.
        var mapping = RouteRequestMapper.ToCreateCommand(Request(downstreamMethod: "FETCH"), "tester");

        mapping.Success.Should().BeFalse();
        mapping.Errors.Should().Contain(e =>
            e.Field == "downstreamMethod" && e.Code == "INVALID_METHOD");
    }

    [Theory]
    [InlineData("1.0")]
    [InlineData("1.1")]
    [InlineData("2.0")]
    public void ToCreateCommand_ShouldCarrySupportedVersions(string version)
    {
        var mapping = RouteRequestMapper.ToCreateCommand(Request(downstreamHttpVersion: version), "tester");

        mapping.Success.Should().BeTrue("the mapping failed: " + string.Join(", ", mapping.Errors.Select(e => e.Field)));
        mapping.Value!.DownstreamHttpVersion.Should().Be(version);
    }

    [Theory]
    [InlineData("3.0")]
    [InlineData("2")]
    [InlineData("2.0.0")]
    public void ToCreateCommand_ShouldCarryTheVersionThroughForTheDomainToJudge(string version)
    {
        // The set of allowed versions is a domain rule, and it is the layer that
        // owns that list. The mapper only refuses a value it cannot read.
        var mapping = RouteRequestMapper.ToCreateCommand(Request(downstreamHttpVersion: version), "tester");

        mapping.Success.Should().BeTrue();
        mapping.Value!.DownstreamHttpVersion.Should().Be(version);
    }

    [Fact]
    public void ToCreateCommand_ShouldCarryTheVersionPolicy()
    {
        var mapping = RouteRequestMapper.ToCreateCommand(
            Request(downstreamHttpVersion: "2.0", downstreamHttpVersionPolicy: "RequestVersionExact"),
            "tester");

        mapping.Success.Should().BeTrue("the mapping failed: " + string.Join(", ", mapping.Errors.Select(e => e.Field)));
        mapping.Value!.DownstreamHttpVersionPolicy.Should().Be("RequestVersionExact");
    }

    [Fact]
    public void ToCreateCommand_ShouldAcceptAPolicyWithoutAVersionForTheDomainToRefuse()
    {
        // The pair is a domain rule, enforced where the aggregate is built.
        // The mapper only rejects values it cannot parse at all.
        var mapping = RouteRequestMapper.ToCreateCommand(
            Request(downstreamHttpVersionPolicy: "RequestVersionExact"),
            "tester");

        mapping.Success.Should().BeTrue();
        mapping.Value!.DownstreamHttpVersionPolicy.Should().Be("RequestVersionExact");
    }

    [Fact]
    public void ToCreateCommand_ShouldCarryAcceptAnyServerCertificate()
    {
        var mapping = RouteRequestMapper.ToCreateCommand(
            Request(acceptAnyServerCertificate: true),
            "tester");

        mapping.Success.Should().BeTrue("the mapping failed: " + string.Join(", ", mapping.Errors.Select(e => e.Field)));
        mapping.Value!.AcceptAnyServerCertificate.Should().BeTrue();
    }

    [Fact]
    public void ToCreateCommand_ShouldCarryDelegatingHandlers()
    {
        var mapping = RouteRequestMapper.ToCreateCommand(
            Request(delegatingHandlers: new List<string> { "First", "Second" }),
            "tester");

        mapping.Success.Should().BeTrue("the mapping failed: " + string.Join(", ", mapping.Errors.Select(e => e.Field)));
        mapping.Value!.DelegatingHandlers.Should().BeEquivalentTo(new[] { "First", "Second" });
    }

    [Fact]
    public void ToCreateCommand_ShouldPassDuplicateDelegatingHandlersOnForTheDomainToRefuse()
    {
        // A duplicate is a domain rule, enforced where the aggregate is built.
        var mapping = RouteRequestMapper.ToCreateCommand(
            Request(delegatingHandlers: new List<string> { "Handler", "handler" }),
            "tester");

        mapping.Success.Should().BeTrue();
        mapping.Value!.DelegatingHandlers.Should().BeEquivalentTo(new[] { "Handler", "handler" });
    }

    [Fact]
    public void ToCreateCommand_ShouldCarryHttpClientOptions()
    {
        var mapping = RouteRequestMapper.ToCreateCommand(
            Request(httpClientOptions: new HttpClientOptionsRequest(
                AllowAutoRedirect: true,
                MaxConnectionsPerServer: 25,
                PooledConnectionLifetimeSeconds: 400,
                UseCookieContainer: true,
                UseProxy: true,
                UseTracing: true)),
            "tester");

        mapping.Success.Should().BeTrue("the mapping failed: " + string.Join(", ", mapping.Errors.Select(e => e.Field)));
        var options = mapping.Value!.HttpClientOptions!;
        options.AllowAutoRedirect.Should().BeTrue();
        options.MaxConnectionsPerServer.Should().Be(25);
        options.PooledConnectionLifetimeSeconds.Should().Be(400);
        options.UseCookieContainer.Should().BeTrue();
        options.UseProxy.Should().BeTrue();
        options.UseTracing.Should().BeTrue();
    }

    [Fact]
    public void ToCreateCommand_ShouldLeaveHttpClientOptionsNullWhenAbsent()
    {
        var mapping = RouteRequestMapper.ToCreateCommand(Request(), "tester");

        mapping.Value!.HttpClientOptions.Should().BeNull();
    }

    [Fact]
    public void ToCreateCommand_ShouldCarryTheTimeout()
    {
        var mapping = RouteRequestMapper.ToCreateCommand(Request(timeoutSeconds: 45), "tester");

        mapping.Success.Should().BeTrue("the mapping failed: " + string.Join(", ", mapping.Errors.Select(e => e.Field)));
        mapping.Value!.TimeoutSeconds.Should().Be(45);
    }

    [Fact]
    public void ToCreateCommand_ShouldPassTheTimeoutOnForTheDomainToRefuse()
    {
        // The domain refuses a non-positive timeout, and the request contract
        // refuses it too. Either way the value never reaches a gateway.
        var mapping = RouteRequestMapper.ToCreateCommand(Request(timeoutSeconds: 0), "tester");

        mapping.Success.Should().BeTrue();
        mapping.Value!.TimeoutSeconds.Should().Be(0);
    }

    private static UpdateRouteRequest UpdateRequest(
        string? downstreamMethod = null,
        string? downstreamPathTemplate = null,
        string? downstreamHttpVersion = null,
        string? downstreamHttpVersionPolicy = null,
        bool acceptAnyServerCertificate = false,
        List<string>? delegatingHandlers = null,
        HttpClientOptionsRequest? httpClientOptions = null,
        int? timeoutSeconds = null) =>
        new(
            Key: "users-list",
            Method: "GET",
            UpstreamPath: "/api/users",
            Host: null,
            ServiceId: Guid.NewGuid().ToString(),
            DownstreamTargets: new List<DownstreamTargetRequest> { new("localhost", 5001, "http", "/") },
            AuthenticationOptions: null,
            AuthorizationOptions: null,
            RateLimitOptions: null,
            QoSOptions: null,
            CacheOptions: null,
            LoadBalancerOptions: null,
            HeaderTransformations: null,
            ClaimTransformations: null,
            QueryTransformations: null,
            DownstreamMethod: downstreamMethod,
            DownstreamPathTemplate: downstreamPathTemplate,
            DownstreamHttpVersion: downstreamHttpVersion,
            DownstreamHttpVersionPolicy: downstreamHttpVersionPolicy,
            AcceptAnyServerCertificate: acceptAnyServerCertificate,
            DelegatingHandlers: delegatingHandlers,
            HttpClientOptions: httpClientOptions,
            TimeoutSeconds: timeoutSeconds);

    [Fact]
    public void ToReplaceCommand_ShouldCarryTheSameFields()
    {
        var mapping = RouteRequestMapper.ToReplaceCommand(
            UpdateRequest(
                downstreamMethod: "POST",
                downstreamHttpVersion: "2.0",
                downstreamHttpVersionPolicy: "RequestVersionOrHigher",
                acceptAnyServerCertificate: true,
                delegatingHandlers: new List<string> { "Handler" },
                httpClientOptions: new HttpClientOptionsRequest(UseTracing: true),
                timeoutSeconds: 90),
            RouteId.New(),
            "tester");

        mapping.Success.Should().BeTrue("the mapping failed: " + string.Join(", ", mapping.Errors.Select(e => e.Field)));
        var command = mapping.Value!;
        command.DownstreamMethod!.Value.Should().Be("POST");
        command.DownstreamHttpVersion.Should().Be("2.0");
        command.DownstreamHttpVersionPolicy.Should().Be("RequestVersionOrHigher");
        command.AcceptAnyServerCertificate.Should().BeTrue();
        command.DelegatingHandlers.Should().BeEquivalentTo(new[] { "Handler" });
        command.HttpClientOptions!.UseTracing.Should().BeTrue();
        command.TimeoutSeconds.Should().Be(90);
    }

    [Theory]
    [InlineData("3.0")]
    [InlineData("latest")]
    public void TheRequestContract_ShouldRejectUnsupportedVersionsAtValidationToo(string version)
    {
        // The mapper is not the only gate. Model binding runs first, and an
        // invalid value there must not reach the handler.
        var request = Request(downstreamHttpVersion: version);
        var context = new ValidationContext(request);
        var results = new List<ValidationResult>();

        Validator.TryValidateObject(request, context, results, validateAllProperties: true).Should().BeFalse();
        results.Should().Contain(r =>
            r.MemberNames.Contains(nameof(CreateRouteRequest.DownstreamHttpVersion)));
    }

    [Theory]
    [InlineData("RequestVersionMaybe")]
    [InlineData("exact")]
    public void TheRequestContract_ShouldRejectAnUnknownPolicyAtValidationToo(string policy)
    {
        var request = Request(downstreamHttpVersion: "2.0", downstreamHttpVersionPolicy: policy);
        var context = new ValidationContext(request);
        var results = new List<ValidationResult>();

        Validator.TryValidateObject(request, context, results, validateAllProperties: true).Should().BeFalse();
        results.Should().Contain(r =>
            r.MemberNames.Contains(nameof(CreateRouteRequest.DownstreamHttpVersionPolicy)));
    }

    [Fact]
    public void TheRequestContract_ShouldRejectANonPositiveTimeoutAtValidationToo()
    {
        var request = Request(timeoutSeconds: -5);
        var context = new ValidationContext(request);
        var results = new List<ValidationResult>();

        Validator.TryValidateObject(request, context, results, validateAllProperties: true).Should().BeFalse();
        results.Should().Contain(r => r.MemberNames.Contains(nameof(CreateRouteRequest.TimeoutSeconds)));
    }

    [Fact]
    public void TheRequestContract_ShouldRejectANonPositiveConnectionLimit()
    {
        // TryValidateObject does not walk into a nested type on its own, so the
        // nested record is validated directly. ASP.NET's automatic validation
        // does recurse, and the domain refuses the value regardless.
        var options = new HttpClientOptionsRequest(MaxConnectionsPerServer: 0);
        var results = new List<ValidationResult>();

        Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true)
            .Should().BeFalse();
        results.Should().NotBeEmpty();
    }

    /// <summary>
    /// On a positional record an attribute without a target lands on the backing
    /// field, where no validator sees it. These assertions fail silently if the
    /// target is dropped, so they are worth keeping.
    /// </summary>
    [Fact]
    public void TheRequestContract_ShouldTargetItsAttributesAtTheProperty()
    {
        foreach (var property in typeof(CreateRouteRequest).GetProperties())
        {
            var attributes = property.GetCustomAttributes(inherit: true)
                .Select(a => a.GetType())
                .ToList();

            attributes.Should().NotContain(typeof(RequiredAttribute),
                "{0} would not be enforced on a positional record without a target", property.Name);
        }

        typeof(CreateRouteRequest).GetProperty(nameof(CreateRouteRequest.DownstreamHttpVersion))!
            .GetCustomAttributes(inherit: true).Should().ContainSingle(a => a is RegularExpressionAttribute);
        typeof(CreateRouteRequest).GetProperty(nameof(CreateRouteRequest.DownstreamHttpVersionPolicy))!
            .GetCustomAttributes(inherit: true).Should().ContainSingle(a => a is RegularExpressionAttribute);
        typeof(CreateRouteRequest).GetProperty(nameof(CreateRouteRequest.TimeoutSeconds))!
            .GetCustomAttributes(inherit: true).Should().ContainSingle(a => a is RangeAttribute);
    }

    [Fact]
    public void ToCreateCommand_ShouldCarryTheDownstreamPathTemplate()
    {
        var mapping = RouteRequestMapper.ToCreateCommand(
            Request(downstreamPathTemplate: "/internal/orders/{orderId}"),
            "tester");

        mapping.Success.Should().BeTrue();
        mapping.Value!.DownstreamTemplate!.Value.Should().Be("/internal/orders/{orderId}");
    }

    [Fact]
    public void ToCreateCommand_ShouldLeaveTheDownstreamPathTemplateNullWhenAbsent()
    {
        // Null forwards the path unchanged, which is Ocelot's /{everything}.
        var mapping = RouteRequestMapper.ToCreateCommand(Request(), "tester");

        mapping.Value!.DownstreamTemplate.Should().BeNull();
    }

    [Theory]
    [InlineData("/api/{}")]
    [InlineData("/api/{unclosed")]
    [InlineData("/api/{has space}")]
    public void ToCreateCommand_ShouldRejectAMalformedPlaceholder(string template)
    {
        var mapping = RouteRequestMapper.ToCreateCommand(
            Request(downstreamPathTemplate: template),
            "tester");

        mapping.Success.Should().BeFalse();
        mapping.Errors.Should().Contain(e =>
            e.Field == "downstreamPathTemplate" && e.Code == "INVALID_DOWNSTREAM_PATH_TEMPLATE");
    }

    [Fact]
    public void ToReplaceCommand_ShouldCarryTheDownstreamPathTemplate()
    {
        var mapping = RouteRequestMapper.ToReplaceCommand(
            UpdateRequest(downstreamPathTemplate: "/internal/{everything}"),
            RouteId.New(),
            "tester");

        mapping.Success.Should().BeTrue();
        mapping.Value!.DownstreamTemplate!.Value.Should().Be("/internal/{everything}");
    }
}
