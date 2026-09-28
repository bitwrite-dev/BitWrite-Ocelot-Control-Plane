using BitWrite.OcelotControl.Domain.Services;
using FluentAssertions;
using Xunit;

namespace BitWrite.OcelotControl.Domain.Tests.Services;

/// <summary>
/// The canonical form feeds snapshot hashing, so any setting that changes what
/// the gateway does has to reach it. Left out, two different configurations
/// hash identically and an integrity check passes on a mismatch.
/// </summary>
public class CanonicalHashCoverageTests
{
    private readonly ConfigurationCanonicalizer _canonicalizer = new();

    public static IEnumerable<object[]> SettingsThatChangeBehaviour()
    {
        yield return new object[] { "Priority", new OcelotRouteConfiguration { Priority = 10 } };
        yield return new object[] { "RouteIsCaseSensitive", new OcelotRouteConfiguration { RouteIsCaseSensitive = true } };
        yield return new object[] { "DownstreamHttpMethod", new OcelotRouteConfiguration { DownstreamHttpMethod = "POST" } };
        yield return new object[] { "DownstreamHttpVersion", new OcelotRouteConfiguration { DownstreamHttpVersion = "2.0" } };
        yield return new object[] { "DownstreamHttpVersionPolicy", new OcelotRouteConfiguration { DownstreamHttpVersionPolicy = "RequestVersionExact" } };
        yield return new object[]
        {
            "DangerousAcceptAnyServerCertificateValidator",
            new OcelotRouteConfiguration { DangerousAcceptAnyServerCertificateValidator = true }
        };
        yield return new object[] { "DelegatingHandlers", new OcelotRouteConfiguration { DelegatingHandlers = new[] { "Handler" } } };
        yield return new object[]
        {
            "HttpHandlerOptions",
            new OcelotRouteConfiguration
            {
                HttpHandlerOptions = new OcelotHttpHandlerOptions { MaxConnectionsPerServer = 42 }
            }
        };
        yield return new object[] { "Timeout", new OcelotRouteConfiguration { Timeout = 60 } };
    }

    [Theory]
    [MemberData(nameof(SettingsThatChangeBehaviour))]
    public void CanonicalText_ShouldDistinguishSettingsThatChangeBehaviour(string field, OcelotRouteConfiguration changed)
    {
        var baseline = Build(new OcelotRouteConfiguration());

        _canonicalizer.Canonicalize(baseline).Should().NotBe(
            _canonicalizer.Canonicalize(Build(changed)),
            "{0} changes what the gateway does, so it has to reach the hash", field);
    }

    [Theory]
    [MemberData(nameof(SettingsThatChangeBehaviour))]
    public void CanonicalJson_ShouldDistinguishSettingsThatChangeBehaviour(string field, OcelotRouteConfiguration changed)
    {
        var baseline = Build(new OcelotRouteConfiguration());

        _canonicalizer.CanonicalizeJson(baseline).Should().NotBe(
            _canonicalizer.CanonicalizeJson(Build(changed)),
            "{0} changes what the gateway does, so it has to reach the hash", field);
    }

    [Fact]
    public void DelegatingHandlerOrder_ShouldNotAffectTheHash()
    {
        // A reordered list is the same configuration.
        var first = Build(new OcelotRouteConfiguration { DelegatingHandlers = new[] { "A", "B" } });
        var second = Build(new OcelotRouteConfiguration { DelegatingHandlers = new[] { "B", "A" } });

        _canonicalizer.Canonicalize(first).Should().Be(_canonicalizer.Canonicalize(second));
        _canonicalizer.CanonicalizeJson(first).Should().Be(_canonicalizer.CanonicalizeJson(second));
    }

    [Fact]
    public void IdenticalSettings_ShouldProduceTheSameHash()
    {
        var first = Build(new OcelotRouteConfiguration { Timeout = 60, Priority = 5 });
        var second = Build(new OcelotRouteConfiguration { Timeout = 60, Priority = 5 });

        _canonicalizer.Canonicalize(first).Should().Be(_canonicalizer.Canonicalize(second));
        _canonicalizer.CanonicalizeJson(first).Should().Be(_canonicalizer.CanonicalizeJson(second));
    }

    private static OcelotConfiguration Build(OcelotRouteConfiguration extra) => new()
    {
        GlobalConfiguration = new OcelotGlobalConfiguration { BaseUrl = "http://localhost:5000" },
        Routes = new List<OcelotRouteConfiguration>
        {
            new()
            {
                UpstreamPathTemplate = "/api/users",
                UpstreamHttpMethod = new[] { "GET" },
                DownstreamPathTemplate = "/{everything}",
                DownstreamScheme = "http"
            },
            extra
        }
    };
}
