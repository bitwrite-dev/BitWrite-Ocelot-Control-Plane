using FluentAssertions;
using BitWrite.OcelotControl.Domain.Aggregates.AuditLog;
using BitWrite.OcelotControl.Domain.Aggregates.GlobalConfiguration;

// Disambiguates from FluentAssertions.License.
using License = BitWrite.OcelotControl.Domain.Aggregates.License.License;
using BitWrite.OcelotControl.Domain.Aggregates.License;
using BitWrite.OcelotControl.Domain.Exceptions;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Domain.ValueObjects.Status;

namespace BitWrite.OcelotControl.Domain.Tests.Aggregates;

/// <summary>
/// Follow-up to the Service and Route factories in #457: the same
/// Create-on-read defect existed in the License, AuditLog and
/// GlobalConfiguration repositories.
/// </summary>
public class ReconstituteFollowUpTests
{
    private static readonly DateTimeOffset Created = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
    private static readonly DateTimeOffset Updated = new(2026, 2, 3, 4, 5, 6, TimeSpan.Zero);

    [Fact]
    public void LicenseReconstitute_ShouldPreserveId()
    {
        var id = LicenseId.New();

        var license = License.Reconstitute(
            id, "Enterprise", "PROD", LicenseStatus.Active,
            Updated.AddYears(1), 10, 100, Created, Updated);

        license.Id.Should().Be(id);
    }

    [Fact]
    public void LicenseReconstitute_ShouldReturnTheSameIdOnEveryRead()
    {
        var id = LicenseId.New();

        var first = License.Reconstitute(id, "E", "P", LicenseStatus.Active, Updated.AddYears(1), 1, 1, Created, Updated);
        var second = License.Reconstitute(id, "E", "P", LicenseStatus.Active, Updated.AddYears(1), 1, 1, Created, Updated);

        first.Id.Should().Be(second.Id);
        first.Id.Should().Be(id);
    }

    [Fact]
    public void LicenseReconstitute_ShouldPreserveLifecycleState()
    {
        var license = License.Reconstitute(
            LicenseId.New(), "E", "P", LicenseStatus.Revoked,
            Updated.AddYears(1), 10, 100, Created, Updated,
            activatedAt: Updated, revokedAt: Updated, revocationReason: "chargeback");

        license.Status.Should().Be(LicenseStatus.Revoked);
        license.ActivatedAt.Should().Be(Updated);
        license.RevokedAt.Should().Be(Updated);
        license.RevocationReason.Should().Be("chargeback");
    }

    [Fact]
    public void LicenseReconstitute_ShouldAcceptAnExpiredLicense()
    {
        // Create rejects an expiration date in the past, so reloading a license
        // that had since expired would throw and the list would fail outright.
        var past = DateTimeOffset.UtcNow.AddDays(-30);

        var act = () => License.Reconstitute(
            LicenseId.New(), "Expired", "PROD", LicenseStatus.Expired,
            past, 1, 1, Created, Updated);

        act.Should().NotThrow();
    }

    [Fact]
    public void LicenseReconstitute_ShouldPreserveFeatures()
    {
        var features = new List<LicenseFeature>
        {
            new() { Key = "sso", Name = "Single sign-on", IsEnabled = true },
            new() { Key = "audit-export", Name = "Audit export", IsEnabled = false },
        };

        var license = License.Reconstitute(
            LicenseId.New(), "E", "P", LicenseStatus.Active,
            Updated.AddYears(1), 1, 1, Created, Updated, features: features);

        license.Features.Should().HaveCount(2);
        license.Features[0].Key.Should().Be("sso");
        license.Features[1].IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void LicenseReconstitute_ShouldRaiseNoDomainEvents()
    {
        License.Reconstitute(LicenseId.New(), "E", "P", LicenseStatus.Active, Updated.AddYears(1), 1, 1, Created, Updated)
            .DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void AuditLogReconstitute_ShouldPreserveIdAndTimestamp()
    {
        var act = () => AuditLog.Reconstitute(
            "audit-1", "admin", "CreateRoute", "Route", "r-1", "Success", Created, "corr-1", "details");

        var entry = act.Should().NotThrow().Subject;

        // Create generated a new id and stamped UtcNow, so an audit trail
        // re-read would show different identities and times every time.
        entry!.Id.Should().Be("audit-1");
        entry.Timestamp.Should().Be(Created);
        entry.CorrelationId.Should().Be("corr-1");
        entry.Details.Should().Be("details");
    }

    [Fact]
    public void AuditLogReconstitute_ShouldBeStableAcrossRepeatedReads()
    {
        var first = AuditLog.Reconstitute("audit-1", "a", "act", "Route", "r-1", "Success", Created);
        var second = AuditLog.Reconstitute("audit-1", "a", "act", "Route", "r-1", "Success", Created);

        first.Id.Should().Be(second.Id);
        first.Timestamp.Should().Be(second.Timestamp);
    }

    [Fact]
    public void AuditLogReconstitute_ShouldRejectAnEmptyId()
    {
        var act = () => AuditLog.Reconstitute("  ", "a", "act", "Route", "r-1", "Success", Created);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void GlobalConfigurationReconstitute_ShouldPreserveEverything()
    {
        var id = Guid.NewGuid();
        var rateLimit = new RateLimitConfig { EnableRateLimiting = true, HttpStatusCode = "429" };
        var qos = new QoSConfig { TimeoutValue = 5000, DurationOfBreak = 30000 };
        var httpHandler = new HttpHandlerConfig { UseProxy = false, Expect100Continue = true };
        var discovery = new ServiceDiscoveryConfig { Provider = "consul", Host = "consul.local", Port = 8500 };

        var config = GlobalConfiguration.Reconstitute(
            id, "http://localhost:5000", "X-Request-ID", "http", 30000,
            rateLimit, qos, httpHandler, discovery, Updated);

        config.Id.Should().Be(id);
        config.BaseUrl.Should().Be("http://localhost:5000");
        config.RequestIdKey.Should().Be("X-Request-ID");
        config.DownstreamScheme.Should().Be("http");
        config.Timeout.Should().Be(30000);
        config.RateLimit.Should().BeSameAs(rateLimit);
        config.QoS.Should().BeSameAs(qos);
        config.HttpHandler.Should().BeSameAs(httpHandler);
        config.ServiceDiscovery.Should().BeSameAs(discovery);
        config.UpdatedAt.Should().Be(Updated);
    }

    [Fact]
    public void GlobalConfigurationReconstitute_ShouldNotResetToDomainDefaults()
    {
        // Create() returns BaseUrl "http://localhost:5000", timeout 90000 and a
        // fresh Guid, so a read followed by a save reverted the stored config.
        var stored = GlobalConfiguration.Reconstitute(
            Guid.NewGuid(), "https://edge.example.com", "X-Custom", "https", 5000,
            null, null, null, null, Updated);

        stored.BaseUrl.Should().Be("https://edge.example.com");
        stored.Timeout.Should().Be(5000);
    }

    [Fact]
    public void GlobalConfigurationReconstitute_ShouldAllowNullNestedConfig()
    {
        var act = () => GlobalConfiguration.Reconstitute(
            Guid.NewGuid(), null, null, null, null, null, null, null, null, Updated);

        act.Should().NotThrow();
    }
}
