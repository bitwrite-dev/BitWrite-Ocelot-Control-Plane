using System.ComponentModel.DataAnnotations;
using System.Reflection;
using BitWrite.OcelotControl.Api.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace BitWrite.OcelotControl.Api.Tests.Controllers;

/// <summary>
/// The settings endpoints, and the protections on them.
/// </summary>
public class SettingsControllerContractTests
{
    private static MethodInfo Endpoint(string name) =>
        typeof(SettingsController).GetMethod(name)!;

    [Fact]
    public void WritingRequiresTheAdminPolicy()
    {
        // The version choice is permanent, so it is not something a lesser role
        // gets to make.
        Endpoint("CompleteFirstRun").GetCustomAttributes<AuthorizeAttribute>()
            .Should().ContainSingle()
            .Which.Policy.Should().Be("Admin");
    }

    [Fact]
    public void UpdatingRequiresTheAdminPolicy()
    {
        Endpoint("Update").GetCustomAttributes<AuthorizeAttribute>()
            .Should().ContainSingle()
            .Which.Policy.Should().Be("Admin");
    }

    [Fact]
    public void ReadingIsOpenLikeEveryOtherEndpoint()
    {
        // A first-run screen has to be able to ask whether setup is outstanding
        // before anyone has credentials. Every existing endpoint is open too, so
        // protecting this one alone would be inconsistent and not more secure.
        Endpoint("Get").GetCustomAttributes<AuthorizeAttribute>().Should().BeEmpty();
    }

    [Fact]
    public void SettingsLiveUnderTheirOwnRoute()
    {
        // Kept out of /global-configuration, which is a different concept with
        // its own page and endpoints.
        typeof(SettingsController).GetCustomAttributes<RouteAttribute>(inherit: true)
            .Select(attribute => attribute.Template)
            .Should().Contain("api/v1/settings");
    }

    [Fact]
    public void TheFirstRunEndpointIsASeparateRouteFromTheOrdinaryUpdate()
    {
        // A separate endpoint is what makes "this is the one-time choice"
        // visible in the contract rather than only in the implementation.
        Endpoint("CompleteFirstRun").GetCustomAttributes<HttpPostAttribute>()
            .Should().ContainSingle()
            .Which.Template.Should().Be("first-run");
    }

    [Fact]
    public void TheUpdateRequestHasNoVersionField()
    {
        // The single most important structural guarantee here: there is no way to
        // reach the version through the ordinary settings update.
        typeof(UpdateSystemSettingsRequest).GetProperties()
            .Select(property => property.Name)
            .Should().NotContain("OcelotVersion");
    }

    [Fact]
    public void TheVersionIsRequiredOnFirstRun()
    {
        var property = typeof(CompleteFirstRunRequest).GetProperty(nameof(CompleteFirstRunRequest.OcelotVersion));

        property.Should().NotBeNull();
        property!.GetCustomAttributes<RequiredAttribute>().Should().ContainSingle();
    }

    [Theory]
    [InlineData(nameof(CompleteFirstRunRequest.PollIntervalSeconds), 5)]
    [InlineData(nameof(CompleteFirstRunRequest.AuditLogRetentionDays), 0)]
    public void TheRangesMatchTheDomain(string propertyName, int allowedMinimum)
    {
        // Duplicating the bounds in the contract is a risk, so they are checked
        // against what the domain will actually accept.
        var property = typeof(CompleteFirstRunRequest).GetProperty(propertyName)!;
        var range = property.GetCustomAttributes<RangeAttribute>().Should().ContainSingle().Subject;

        range.Minimum.Should().Be(allowedMinimum);
    }

    [Fact]
    public void EveryValidationAttributeTargetsThePropertyRatherThanTheBackingField()
    {
        // On a positional record an attribute without a [property:] target lands
        // on the backing field, where the validator never looks. That failure is
        // silent: the endpoint simply stops rejecting bad input, and only the
        // domain catches it later.
        var ranged = new[]
        {
            (typeof(CompleteFirstRunRequest), nameof(CompleteFirstRunRequest.PollIntervalSeconds)),
            (typeof(CompleteFirstRunRequest), nameof(CompleteFirstRunRequest.AuditLogRetentionDays)),
            (typeof(CompleteFirstRunRequest), nameof(CompleteFirstRunRequest.SnapshotRetentionCount)),
            (typeof(UpdateSystemSettingsRequest), nameof(UpdateSystemSettingsRequest.PollIntervalSeconds)),
            (typeof(UpdateSystemSettingsRequest), nameof(UpdateSystemSettingsRequest.AuditLogRetentionDays)),
            (typeof(UpdateSystemSettingsRequest), nameof(UpdateSystemSettingsRequest.SnapshotRetentionCount)),
        };

        foreach (var (request, propertyName) in ranged)
        {
            request.GetProperty(propertyName)!.GetCustomAttributes<RangeAttribute>()
                .Should().ContainSingle(
                    "{0}.{1} carries a range in the source, so it must reach the property",
                    request.Name, propertyName);
        }

        typeof(CompleteFirstRunRequest).GetProperty(nameof(CompleteFirstRunRequest.OcelotVersion))!
            .GetCustomAttributes<MaxLengthAttribute>().Should().ContainSingle();
    }
}
