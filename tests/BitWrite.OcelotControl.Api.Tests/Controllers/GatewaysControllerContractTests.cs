using System.Reflection;
using BitWrite.OcelotControl.Api.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace BitWrite.OcelotControl.Api.Tests.Controllers;

/// <summary>
/// The gateway deletion-eligibility endpoint.
///
/// Deletion is refused once a publication has been addressed to a gateway,
/// because a published snapshot is an immutable hashed record referring to it.
/// This endpoint exists so the rule is reported before the operator confirms a
/// deletion, rather than by the refusal afterwards.
/// </summary>
public class GatewaysControllerContractTests
{
    private static MethodInfo Endpoint(string name) =>
        typeof(GatewaysController).GetMethod(name)!;

    [Fact]
    public void DeletionEligibilityIsItsOwnEndpoint()
    {
        // Folding this into the list would mean fetching it for every gateway on
        // the page to answer a question most operators never ask.
        Endpoint("GetDeletionEligibility").GetCustomAttributes<HttpGetAttribute>()
            .Should().ContainSingle()
            .Which.Template.Should().Be("{id}/deletion-eligibility");
    }

    [Fact]
    public void TheHandlerIsInjected()
    {
        typeof(GatewaysController)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Select(parameter => parameter.ParameterType.Name)
            .Should().Contain("GetGatewayDeletionEligibilityQueryHandler");
    }

    [Fact]
    public void DeleteStillReturnsNoContent()
    {
        // The eligibility endpoint is advisory. The rule is enforced on the
        // aggregate, so passing the wrong answer cannot delete a gateway that
        // should be kept.
        Endpoint("DeleteGateway").GetCustomAttributes<HttpDeleteAttribute>()
            .Should().ContainSingle();
    }

    [Fact]
    public void TheEligibilityResponseCarriesTheReason()
    {
        // A refusal without a reason is an unexplained refusal.
        typeof(Application.UseCases.Gateway.GatewayDeletionEligibilityResponse)
            .GetProperties()
            .Select(property => property.Name)
            .Should().Contain(new[] { "GatewayId", "HasBeenPublishedTo", "Reason" });
    }
}
