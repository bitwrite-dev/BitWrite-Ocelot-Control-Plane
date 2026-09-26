using FluentAssertions;
using BitWrite.OcelotControl.Domain.Exceptions;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using Xunit;

namespace BitWrite.OcelotControl.Domain.Tests.ValueObjects;

/// <summary>
/// Ocelot placeholders such as <c>/api/{everything}</c> are the most common shape
/// a proxy route takes, and <see cref="UpstreamPath"/> rejected them outright, so
/// a catch-all route could not be created at all. See #459.
///
/// The path pattern is kept strict by validating each placeholder separately, so
/// malformed braces are still refused.
/// </summary>
public class UpstreamPathPlaceholderTests
{
    [Theory]
    [InlineData("/api/{everything}")]
    [InlineData("/api/{catchAll}")]
    [InlineData("/api/v1/{everythingElse}")]
    [InlineData("/api/{everything}/details")]
    [InlineData("/{everything}")]
    [InlineData("/api/things/{id}")]
    [InlineData("/api/{_private}")]
    public void ShouldAcceptWellFormedPlaceholders(string path)
    {
        var act = () => UpstreamPath.From(path);

        act.Should().NotThrow();
        act().Value.Should().Be(path);
    }

    [Theory]
    [InlineData("/api/{}")]           // empty name
    [InlineData("/api/{everything")]   // unclosed
    [InlineData("/api/everything}")]   // unopened
    [InlineData("/api/{has space}")]   // illegal character
    [InlineData("/api/{dash-name}")]   // hyphen is not a legal identifier character
    [InlineData("/api/{1digit}")]      // must not start with a digit
    public void ShouldRejectMalformedPlaceholders(string path)
    {
        var act = () => UpstreamPath.From(path);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void ShouldReportPlaceholderProblemsAsTheirOwnErrorCode()
    {
        // Distinguishable from a generic format failure, so a caller can tell the
        // user which problem to fix.
        var act = () => UpstreamPath.From("/api/{}");

        act.Should().Throw<DomainException>()
            .Which.ErrorCode.Should().Be("INVALID_UPSTREAM_PATH_PLACEHOLDER");
    }

    [Fact]
    public void ShouldStillRejectAPathWithIllegalCharactersOutsidePlaceholders()
    {
        // Stripping placeholders must not weaken validation of the rest of the path.
        var act = () => UpstreamPath.From("/api/{everything}/bad<segment");

        act.Should().Throw<DomainException>()
            .Which.ErrorCode.Should().Be("INVALID_UPSTREAM_PATH_FORMAT");
    }

    [Fact]
    public void ShouldStillRejectAnEmptyPath()
    {
        var act = () => UpstreamPath.From("   ");

        act.Should().Throw<DomainException>()
            .Which.ErrorCode.Should().Be("INVALID_UPSTREAM_PATH");
    }

    [Fact]
    public void ShouldStillNormaliseAMissingLeadingSlash()
    {
        UpstreamPath.From("api/{everything}").Value.Should().Be("/api/{everything}");
    }

    [Theory]
    [InlineData("/api/users")]     // no placeholder, unchanged behaviour
    [InlineData("/api/users/{id}")]
    public void ShouldKeepWorkingForExistingValidPaths(string path)
    {
        var act = () => UpstreamPath.From(path);

        act.Should().NotThrow();
    }

    [Fact]
    public void AppendShouldPreservePlaceholders()
    {
        var act = () => UpstreamPath.From("/api/{everything}").Append("details");

        act.Should().NotThrow();
        act().Value.Should().Be("/api/{everything}/details");
    }

    [Fact]
    public void ShouldRejectAMalformedPlaceholderAddedByAppend()
    {
        var act = () => UpstreamPath.From("/api/users").Append("{unclosed");

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void PlaceholdersShouldNotAffectEquality()
    {
        // Two paths differing only in placeholder name describe the same route
        // shape; this was already how GetEqualityComponents worked, since it
        // compares the raw value.
        UpstreamPath.From("/api/{everything}").Should().Be(UpstreamPath.From("/api/{everything}"));
    }
}
