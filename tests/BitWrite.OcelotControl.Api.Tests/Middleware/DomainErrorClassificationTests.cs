using BitWrite.OcelotControl.Api.Middleware;
using BitWrite.OcelotControl.Domain.Exceptions;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace BitWrite.OcelotControl.Api.Tests.Middleware;

/// <summary>
/// Turning a domain rule violation into an answer a caller can act on.
/// </summary>
/// <remarks>
/// <c>DomainException</c> derives from <see cref="Exception"/>, so before this every
/// rule violation in the product answered 500 with its message replaced. A
/// rejected configuration and a broken server looked identical, and the
/// <c>ErrorCode</c> — the machine-readable half — never left the process.
/// </remarks>
public class DomainErrorClassificationTests
{
    [Theory]
    // The 105 INVALID_ codes are the bulk of the domain's vocabulary and are all
    // a bad request.
    [InlineData("INVALID_ROUTE_ID", 400, "Validation")]
    [InlineData("INVALID_DOWNSTREAM_PORT", 400, "Validation")]
    [InlineData("INVALID_AUTH_SCHEME", 400, "Validation")]
    [InlineData("INVALID_CACHE_TTL", 400, "Validation")]
    [InlineData("MISSING_OCELOT_VERSION", 400, "Validation")]
    [InlineData("FEATURE_NOT_SUPPORTED", 409, "Conflict")]
    [InlineData("NOT_SUPPORTED_VERSION", 409, "Conflict")]
    [InlineData("ROUTE_NOT_FOUND", 404, "NotFound")]
    [InlineData("SERVICE_NOT_FOUND", 404, "NotFound")]
    [InlineData("ENDPOINT_NOT_FOUND", 404, "NotFound")]
    [InlineData("ROUTE_CONFLICT", 409, "Conflict")]
    [InlineData("DUPLICATE_ENDPOINT", 409, "Conflict")]
    [InlineData("DUPLICATE_CAPABILITY", 409, "Conflict")]
    [InlineData("OCELOT_VERSION_ALREADY_CHOSEN", 409, "Conflict")]
    [InlineData("ALREADY_ROLLED_BACK", 409, "Conflict")]
    [InlineData("GATEWAY_HAS_PUBLICATION_HISTORY", 409, "Conflict")]
    [InlineData("NO_TARGET_GATEWAYS", 409, "Conflict")]
    [InlineData("NO_DOWNSTREAM_TARGETS", 409, "Conflict")]
    [InlineData("TOO_MANY_DOWNSTREAM_TARGETS", 409, "Conflict")]
    public void MapsEachKindOfRuleToTheStatusItMeans(
        string errorCode,
        int expectedStatus,
        string expectedKind)
    {
        DomainErrorClassification.StatusFor(errorCode).Should().Be(expectedStatus);
        DomainErrorClassification.KindFor(errorCode).Should().Be(expectedKind);
    }

    [Fact]
    public void TreatsAConflictAsRetryableRatherThanMalformed()
    {
        // A route that already exists is not a malformed request — the caller sent
        // something perfectly well formed, and 400 would tell them to fix the
        // request rather than to pick a different name.
        DomainErrorClassification.StatusFor("DUPLICATE_ENDPOINT")
            .Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public void MapsAnUnknownCodeToABadRequestRatherThanAServerFault()
    {
        // A rule added tomorrow should not be reported as a broken server on its
        // first day, which is what the catch-all used to do. The message still
        // reaches the caller either way.
        DomainErrorClassification.StatusFor("SOMETHING_NOBODY_INVENTED_YET")
            .Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public void SurvivesAnEmptyOrMissingCode()
    {
        // A code is optional on a hand-thrown exception, and a null
        // dereference here would replace a rejected request with a crash.
        DomainErrorClassification.StatusFor(string.Empty)
            .Should().Be(StatusCodes.Status400BadRequest);
        DomainErrorClassification.StatusFor(null!)
            .Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public void KeepsTheMessageTheDomainWrote()
    {
        // This text is the product's own explanation, written for the operator
        // reading the screen — which versions can be targeted, why a choice cannot
        // be undone. Discarding it is what made a rejected configuration
        // indistinguishable from a broken server.
        var exception = new DomainException(
            "Ocelot 20.0.0 cannot be targeted yet: its configuration shapes are not " +
            "established. 18.0.0 can.",
            "OCELOT_VERSION_NOT_EMITTABLE");

        DomainErrorClassification.MessageFor(exception)
            .Should().Contain("18.0.0 can");
    }
}
