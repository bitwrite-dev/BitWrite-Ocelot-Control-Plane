using BitWrite.OcelotControl.Domain.Exceptions;
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
/// Emitting authorization and transformations into the generated configuration.
/// </summary>
/// <remarks>
/// Two things are under test: that a value Ocelot 18 can express reaches the
/// output, and that one it cannot is refused. The second matters more — a rule
/// that is quietly dropped is the failure mode this area has been fighting.
/// </remarks>
public class FeatureOptionEmissionTests
{
    private readonly ConfigurationBuilder _builder = new(new ConfigurationCanonicalizer());

    private RouteConfiguration Input(
        AuthorizationOptions? authorization = null,
        HeaderOptions? headers = null,
        ClaimOptions? claims = null,
        QueryOptions? query = null)
    {
        var route = Domain.Aggregates.Route.Route.Create(
            HttpMethod.Get,
            UpstreamPath.From("/api/users"),
            ServiceId.New(),
            new List<DownstreamTarget> { DownstreamTarget.Create("http", "localhost", 5001) },
            "users-list");

        return new RouteConfiguration
        {
            Id = route.Id,
            Host = route.Host,
            Method = route.Method,
            UpstreamPath = route.UpstreamPath,
            ServiceId = route.ServiceId,
            DownstreamTargets = route.DownstreamTargets
                .Select(t => DownstreamTarget.Create(t.Scheme, t.Host, t.Port, t.Path))
                .ToList(),
            AuthorizationOptions = authorization,
            HeaderOptions = headers,
            ClaimOptions = claims,
            QueryOptions = query
        };
    }

    private OcelotRouteConfiguration Emit(RouteConfiguration input) =>
        _builder.BuildConfiguration(
            new List<RouteConfiguration> { input },
            new MinimalGlobalConfig { BaseUrl = "", RequestIdKey = "" },
            ConfigurationBuilder.BaselineVersion).Routes.Single();

    [Fact]
    public void Authorization_ShouldBecomeAClaimsRequirement()
    {
        var emitted = Emit(Input(authorization: AuthorizationOptions.Create(
            requirements: new Dictionary<string, string>
            {
                ["role"] = "admin",
                ["permission"] = "users.read"
            })));

        emitted.RouteClaimsRequirement.Should().NotBeNull();
        emitted.RouteClaimsRequirement!.Claims.Should().Contain("role", "admin");
        emitted.RouteClaimsRequirement.Claims.Should().Contain("permission", "users.read");
    }

    [Fact]
    public void Authorization_ShouldEmitNothing_WhenThereAreNoRequirements()
    {
        var emitted = Emit(Input(authorization: AuthorizationOptions.Create(
            requirements: new Dictionary<string, string>())));

        emitted.RouteClaimsRequirement.Should().BeNull();
    }

    [Fact]
    public void Authorization_ShouldBeRefused_WhenAPolicyIsConfigured()
    {
        // Ocelot 18 has no route-level policy. Emitting it as nothing would leave
        // an operator believing a rule is in force.
        var act = () => Emit(Input(authorization: AuthorizationOptions.Create(
            new List<string> { "RequireAdmin" })));

        act.Should().Throw<NotExpressibleException>()
            .Which.Field.Should().Be("authorizationOptions.policies");
    }

    [Fact]
    public void Authorization_ShouldBeRefused_WhenScopesAreConfigured()
    {
        // In 18, scopes belong to AuthenticationOptions.AllowedScopes, which the
        // route already carries separately.
        var act = () => Emit(Input(authorization: AuthorizationOptions.Create(
            scopes: new List<string> { "admin" })));

        act.Should().Throw<NotExpressibleException>()
            .Which.Field.Should().Be("authorizationOptions.scopes");
    }

    [Fact]
    public void HeaderTransform_ShouldSplitIntoUpstreamAndDownstream()
    {
        var emitted = Emit(Input(headers: HeaderOptions.Create(
            add: new List<HeaderTransform> { HeaderTransform.Create("X-Downstream", "orders") },
            transform: new List<HeaderTransform>
            {
                HeaderTransform.Create("X-Original-Host", "{UpstreamHost}")
            })));

        emitted.DownstreamHeaderTransform.Should().Contain("X-Downstream", "orders");
        emitted.UpstreamHeaderTransform.Should().Contain("X-Original-Host", "{UpstreamHost}");
    }

    [Fact]
    public void HeaderRemoval_ShouldBeRefused()
    {
        var act = () => Emit(Input(headers: HeaderOptions.Create(
            remove: new List<string> { "X-Debug" })));

        act.Should().Throw<NotExpressibleException>()
            .Which.Field.Should().Be("headerTransformations.remove");
    }

    [Fact]
    public void Claims_ShouldBecomeAddClaimsToRequest()
    {
        var emitted = Emit(Input(claims: ClaimOptions.Create(
            add: new List<ClaimTransform> { ClaimTransform.Create("sub", "user-1") })));

        emitted.AddClaimsToRequest.Should().Contain("sub", "user-1");
    }

    [Fact]
    public void ClaimRemoval_ShouldBeRefused()
    {
        var act = () => Emit(Input(claims: ClaimOptions.Create(
            remove: new List<string> { "stale" })));

        act.Should().Throw<NotExpressibleException>()
            .Which.Field.Should().Be("claimTransformations.remove");
    }

    [Fact]
    public void QueryTransformation_ShouldBeRefused()
    {
        // A route's query string is shaped by its path template in 18. The only
        // query block is claim-sourced, which is not what this describes.
        var act = () => Emit(Input(query: QueryOptions.Create(
            add: new List<QueryTransform> { QueryTransform.Create("apiKey", "$1") })));

        act.Should().Throw<NotExpressibleException>()
            .Which.Field.Should().Be("queryTransformations");
    }

    [Fact]
    public void AnEmptyQueryBlock_ShouldNotBeRefused()
    {
        // Nothing configured means nothing to express, not a configuration error.
        var act = () => Emit(Input(query: QueryOptions.Create()));

        act.Should().NotThrow();
    }

    [Fact]
    public void AnEmptyTransformationBlock_ShouldEmitNothing()
    {
        var emitted = Emit(Input(
            headers: HeaderOptions.Create(),
            claims: ClaimOptions.Create()));

        emitted.DownstreamHeaderTransform.Should().BeNull();
        emitted.UpstreamHeaderTransform.Should().BeNull();
        emitted.AddClaimsToRequest.Should().BeNull();
    }

    [Fact]
    public void ARepeatedKey_ShouldBeRefused()
    {
        // Two entries with one key would silently lose one of them.
        var act = () => Emit(Input(headers: HeaderOptions.Create(
            add: new List<HeaderTransform>
            {
                HeaderTransform.Create("X-Trace", "$1"),
                HeaderTransform.Create("X-Trace", "$2"),
            })));

        act.Should().Throw<NotExpressibleException>()
            .Which.Message.Should().Contain("listed more than once");
    }

    [Fact]
    public void TheBaseline_ShouldBeEighteen()
    {
        // Stated once, so the version the output targets is not a per-call guess.
        ConfigurationBuilder.BaselineVersion.Should().Be(OcelotVersion.V18_0);
    }

    [Fact]
    public void ARefusal_ShouldCarryAFieldAndACode()
    {
        var act = () => Emit(Input(authorization: AuthorizationOptions.Create(
            new List<string> { "RequireAdmin" })));

        act.Should().Throw<NotExpressibleException>()
            .Which.ErrorCode.Should().Be("NOT_EXPRESSIBLE");
    }
}
