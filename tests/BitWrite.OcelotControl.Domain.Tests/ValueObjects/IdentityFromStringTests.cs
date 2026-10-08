using BitWrite.OcelotControl.Domain.Exceptions;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using FluentAssertions;
using Xunit;

namespace BitWrite.OcelotControl.Domain.Tests.ValueObjects;

/// <summary>
/// Every identifier, refused properly when the text is not one.
/// </summary>
/// <remarks>
/// All four controllers parsed the GUID themselves before handing it to the value
/// object — <c>RouteId.From(Guid.Parse(id))</c> — so a malformed URL threw
/// <see cref="FormatException"/>, which no error path recognised, and answered 500.
/// The string overloads below have always done the right thing; nothing called them.
///
/// A shared test rather than four copies: the four overloads are identical, and the
/// thing worth protecting is that they stay that way.
/// </remarks>
public class IdentityFromStringTests
{
    private static readonly string Valid = Guid.NewGuid().ToString();

    [Theory]
    [InlineData("-1")]
    [InlineData("not-a-guid")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("00000000-0000-0000-0000-00000000000")]
    public void RejectsTextThatIsNotAGuidWithACodeTheClientCanBranchOn(string value)
    {
        // A 500 told an operator the server was broken and invited a retry that
        // could not succeed. The code is the half a dashboard needs.
        Action routeId = () => RouteId.From(value);
        Action gatewayId = () => GatewayId.From(value);
        Action serviceId = () => ServiceId.From(value);
        Action licenseId = () => LicenseId.From(value);

        foreach (var parse in new[] { routeId, gatewayId, serviceId, licenseId })
        {
            parse.Should().Throw<DomainException>()
                .Which.ErrorCode.Should().BeOneOf(
                    "INVALID_ROUTE_ID_FORMAT",
                    "INVALID_GATEWAY_ID_FORMAT",
                    "INVALID_SERVICE_ID_FORMAT",
                    "INVALID_LICENSE_ID_FORMAT");
        }
    }

    [Fact]
    public void RejectsTheEmptyGuidSeparatelyFromBadText()
    {
        // Parses, then is refused as empty. Different code, different meaning: this
        // is a well-formed id that names nothing.
        Action parse = () => RouteId.From(Guid.Empty.ToString());

        parse.Should().Throw<DomainException>()
            .Which.ErrorCode.Should().Be("INVALID_ROUTE_ID");
    }

    [Fact]
    public void AcceptsTextThatIsAGuid()
    {
        RouteId.From(Valid).Value.Should().NotBe(Guid.Empty);
        GatewayId.From(Valid).Value.Should().NotBe(Guid.Empty);
        ServiceId.From(Valid).Value.Should().NotBe(Guid.Empty);
        LicenseId.From(Valid).Value.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void ProducesTheSameIdentifierAsTheGuidOverload()
    {
        // The two overloads have to agree, or swapping one for the other would
        // quietly change which snapshot, gateway or license a URL resolves to.
        var guid = Guid.NewGuid();

        RouteId.From(guid.ToString()).Value.Should().Be(RouteId.From(guid).Value);
        GatewayId.From(guid.ToString()).Value.Should().Be(GatewayId.From(guid).Value);
        ServiceId.From(guid.ToString()).Value.Should().Be(ServiceId.From(guid).Value);
        LicenseId.From(guid.ToString()).Value.Should().Be(LicenseId.From(guid).Value);
    }
}
