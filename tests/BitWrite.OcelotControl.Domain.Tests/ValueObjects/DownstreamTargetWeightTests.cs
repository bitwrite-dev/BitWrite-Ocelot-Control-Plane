using BitWrite.OcelotControl.Domain.Exceptions;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using FluentAssertions;
using Xunit;

namespace BitWrite.OcelotControl.Domain.Tests.ValueObjects;

/// <summary>
/// A downstream target's weight, and how "the same destination" is decided.
/// </summary>
/// <remarks>
/// A service endpoint is weighted for load balancing and the weight was in the domain
/// all along — but not in the API request, so it could neither be seen nor set, and
/// the update handler matched endpoints on host and port alone and dropped it.
/// <para>
/// <c>AddressesSameAs</c> exists because record equality is the wrong tool for the
/// question the duplicate checks ask: a record compares every property, so two
/// targets pointing at one address under different weights compare unequal, and
/// folding weight into <c>GetEqualityComponents</c> would not change that — that
/// method only feeds <c>GetHashCode</c>.
/// </para>
/// </remarks>
public class DownstreamTargetWeightTests
{
    [Fact]
    public void DefaultsToOne()
    {
        // A route's target has no weight, and every existing caller omits it.
        DownstreamTarget.Create("http", "localhost", 5001).Weight.Should().Be(1);
    }

    [Fact]
    public void CarriesTheWeightItWasGiven()
    {
        DownstreamTarget.Create("http", "localhost", 5001, "/", weight: 5).Weight.Should().Be(5);
    }

    [Fact]
    public void RejectsAWeightThatIsNotPositive()
    {
        Action zero = () => DownstreamTarget.Create("http", "localhost", 5001, "/", weight: 0);
        Action negative = () => DownstreamTarget.Create("http", "localhost", 5001, "/", weight: -3);

        zero.Should().Throw<DomainException>().Which.ErrorCode.Should().Be("INVALID_WEIGHT");
        negative.Should().Throw<DomainException>().Which.ErrorCode.Should().Be("INVALID_WEIGHT");
    }

    [Fact]
    public void SaysWhetherTwoTargetsAddressTheSameDestinationRegardlessOfWeight()
    {
        // The duplicate checks need this: they ask whether an address is already
        // listed, and the answer must not depend on how heavily it is weighted. A
        // record compares every property, so record equality is not the question to
        // ask here — and asserting `Be` would have said something untrue.
        var light = DownstreamTarget.Create("http", "localhost", 5001, "/", weight: 1);
        var heavy = DownstreamTarget.Create("http", "localhost", 5001, "/", weight: 10);

        light.AddressesSameAs(heavy).Should().BeTrue();
        light.Should().NotBe(heavy, "record equality does see every property");
    }

    [Fact]
    public void AnswersTheAddressQuestionAboutDifferentAddresses()
    {
        var baseline = DownstreamTarget.Create("http", "localhost", 5001);

        baseline.AddressesSameAs(DownstreamTarget.Create("http", "localhost", 5002))
            .Should().BeFalse();
        baseline.AddressesSameAs(DownstreamTarget.Create("http", "elsewhere", 5001))
            .Should().BeFalse();
        baseline.AddressesSameAs(DownstreamTarget.Create("https", "localhost", 5001))
            .Should().BeFalse();
        baseline.AddressesSameAs(null!).Should().BeFalse();
    }

    [Fact]
    public void StillDistinguishesTargetsThatAddressDifferentThings()
    {
        // The other half: excluding weight must not have made equality vacuous.
        DownstreamTarget.Create("http", "localhost", 5001)
            .Should().NotBe(DownstreamTarget.Create("http", "localhost", 5002));
        DownstreamTarget.Create("http", "localhost", 5001, "/a")
            .Should().NotBe(DownstreamTarget.Create("http", "localhost", 5001, "/b"));
        DownstreamTarget.Create("https", "localhost", 5001)
            .Should().NotBe(DownstreamTarget.Create("http", "localhost", 5001));
    }

    [Fact]
    public void DoesNotAppearInTheAddress()
    {
        // A weight is not part of where traffic goes, so it must not turn up in the
        // URI — which is what a gateway is eventually handed.
        DownstreamTarget.Create("http", "localhost", 5001, "/", weight: 7)
            .ToUri().Should().Be("http://localhost:5001/");
    }
}
