using BitWrite.OcelotControl.Api.DTOs;
using BitWrite.OcelotControl.Api.Mapping;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using FluentAssertions;
using Xunit;

namespace BitWrite.OcelotControl.Api.Tests.Mapping;

/// <summary>
/// The request contract for authorization and transformations.
/// </summary>
/// <remarks>
/// The domain supported all of this from the start; it simply had no way in
/// through the API, so anything a user entered was discarded on save. See #466.
/// </remarks>
public class RouteRequestMapperAuthTests
{
    /// <summary>A minimal valid create request, optionally adjusted per test.</summary>
    private static CreateRouteRequest Request(
        AuthorizationOptionsRequest? authorization = null,
        AuthenticationOptionsRequest? authentication = null,
        TransformationsRequest? headers = null,
        TransformationsRequest? claims = null,
        TransformationsRequest? query = null) =>
        new(
            "users-list",
            "GET",
            "/api/users",
            null,
            Guid.NewGuid().ToString(),
            new List<DownstreamTargetRequest> { new("localhost", 5001, "http", "/") },
            authentication,
            authorization,
            null,
            null,
            null,
            null,
            headers,
            claims,
            query);

    [Fact]
    public void ToCreateCommand_ShouldCarryAuthorization()
    {
        var request = Request(authorization: new AuthorizationOptionsRequest(
            new List<string> { "RequireAdmin" },
            new List<string> { "admin" },
            new Dictionary<string, string> { ["role"] = "admin" }));

        var mapping = RouteRequestMapper.ToCreateCommand(request, "tester");

        mapping.Success.Should().BeTrue();
        mapping.Value!.AuthorizationOptions.Should().NotBeNull();
        mapping.Value.AuthorizationOptions!.Policies.Should().ContainSingle("RequireAdmin");
        mapping.Value.AuthorizationOptions.Scopes.Should().ContainSingle("admin");
        mapping.Value.AuthorizationOptions.Requirements["role"].Should().Be("admin");
    }

    [Fact]
    public void ToCreateCommand_ShouldLeaveAuthorizationNullWhenAbsent()
    {
        var mapping = RouteRequestMapper.ToCreateCommand(Request(), "tester");

        mapping.Success.Should().BeTrue();
        mapping.Value!.AuthorizationOptions.Should().BeNull();
    }

    [Fact]
    public void ToCreateCommand_ShouldCarryHeaderTransformations()
    {
        var request = Request(headers: new TransformationsRequest(
            new List<TransformEntryRequest> { new("X-Api-Key", "secret") },
            new List<string> { "X-Debug" },
            new List<TransformEntryRequest> { new("X-Trace", "$1") }));

        var mapping = RouteRequestMapper.ToCreateCommand(request, "tester");

        mapping.Value!.HeaderOptions.Should().NotBeNull();
        mapping.Value.HeaderOptions!.Add.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { Key = "X-Api-Key", Value = "secret" });
        mapping.Value.HeaderOptions.Remove.Should().ContainSingle("X-Debug");
        mapping.Value.HeaderOptions.Transform.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { Key = "X-Trace", Value = "$1" });
    }

    [Fact]
    public void ToCreateCommand_ShouldCarryClaimAndQueryTransformations()
    {
        var request = Request(
            claims: new TransformationsRequest(
                new List<TransformEntryRequest> { new("sub", "user-1") }, null, null),
            query: new TransformationsRequest(
                null,
                new List<string> { "debug" },
                new List<TransformEntryRequest> { new("apiKey", "$1") }));

        var mapping = RouteRequestMapper.ToCreateCommand(request, "tester");

        mapping.Value!.ClaimOptions.Should().NotBeNull();
        mapping.Value.ClaimOptions!.Add.Should().ContainSingle();
        mapping.Value.QueryOptions.Should().NotBeNull();
        mapping.Value.QueryOptions!.Remove.Should().ContainSingle("debug");
    }

    [Fact]
    public void ToCreateCommand_ShouldReportABlankTransformKeyAgainstItsField()
    {
        var request = Request(headers: new TransformationsRequest(
            // The domain rejects a blank key, so the failure has to name the
            // block that caused it.
            new List<TransformEntryRequest> { new("  ", "value") }, null, null));

        var mapping = RouteRequestMapper.ToCreateCommand(request, "tester");

        mapping.Success.Should().BeFalse();
        mapping.Errors.Should().Contain(e => e.Field == "headerTransformations");
    }

    [Fact]
    public void ToReplaceCommand_ShouldCarryEveryNewBlock()
    {
        var id = RouteId.New();
        var request = new UpdateRouteRequest(
            Key: "users-list",
            Method: "GET",
            UpstreamPath: "/api/users",
            Host: null,
            ServiceId: Guid.NewGuid().ToString(),
            DownstreamTargets: new List<DownstreamTargetRequest> { new("localhost", 5001, "http", "/") },
            AuthenticationOptions: null,
            AuthorizationOptions: new AuthorizationOptionsRequest(new List<string> { "P" }, null, null),
            RateLimitOptions: null,
            QoSOptions: null,
            CacheOptions: null,
            LoadBalancerOptions: null,
            HeaderTransformations: new TransformationsRequest(null, new List<string> { "X-Debug" }, null),
            ClaimTransformations: new TransformationsRequest(null, new List<string> { "stale" }, null),
            QueryTransformations: new TransformationsRequest(null, null, null));

        var mapping = RouteRequestMapper.ToReplaceCommand(request, id, "tester");

        mapping.Success.Should().BeTrue();
        mapping.Value!.AuthorizationOptions.Should().NotBeNull();
        mapping.Value.HeaderOptions.Should().NotBeNull();
        mapping.Value.ClaimOptions.Should().NotBeNull();
        // An all-null transformation block is still a configured block, so it is
        // kept rather than treated as absent.
        mapping.Value.QueryOptions.Should().NotBeNull();
    }

    [Fact]
    public void ToValidationInput_ShouldReportTheNewCapabilitiesInUse()
    {
        // The validator checks the capabilities a route actually uses, so a
        // draft with authorization has to declare it or the check is skipped.
        var request = Request(
            authorization: new AuthorizationOptionsRequest(new List<string> { "P" }, null, null),
            headers: new TransformationsRequest(null, new List<string> { "X-Debug" }, null));

        var mapping = RouteRequestMapper.ToValidationInput(request);

        mapping.Success.Should().BeTrue();
        mapping.Value!.Features.Should().Contain("authorization");
        mapping.Value.Features.Should().Contain("header-transformation");
    }

    [Fact]
    public void ToValidationInput_ShouldNotDeclareCapabilitiesThatAreOff()
    {
        var mapping = RouteRequestMapper.ToValidationInput(Request());

        mapping.Value!.Features.Should().NotContain("authorization");
        mapping.Value.Features.Should().NotContain("header-transformation");
    }

    [Fact]
    public void Authorization_ShouldNotBeConflatedWithAuthentication()
    {
        // The wizard's Authentication step writes allowedScopes; a separate
        // Authorization step writes policies and requirements. One must not
        // populate the other.
        var request = Request(
            authentication: new AuthenticationOptionsRequest(new List<string> { "users.read" }),
            authorization: new AuthorizationOptionsRequest(new List<string> { "RequireAdmin" }, null, null));

        var mapping = RouteRequestMapper.ToCreateCommand(request, "tester");

        mapping.Value!.AuthenticationOptions!.Properties["scopes"]
            .Should().Be("users.read");
        mapping.Value.AuthenticationOptions.Properties.Should().NotContainKey("policies");
        mapping.Value.AuthorizationOptions!.Policies.Should().ContainSingle("RequireAdmin");
    }
}
