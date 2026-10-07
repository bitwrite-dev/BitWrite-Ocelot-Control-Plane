using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Infrastructure.Redis;
using FluentAssertions;
using Xunit;

namespace BitWrite.OcelotControl.Infrastructure.Tests.Redis;

public class RedisKeyHelperTests
{
    /// <summary>The environment every key below is built for.</summary>
    private static readonly EnvironmentName Development = EnvironmentName.From("development");

    [Fact]
    public void Gateway_ShouldGenerateCorrectKey()
    {
        var gatewayId = GatewayId.New();
        var key = RedisKeyHelper.Gateway(gatewayId);
        key.Should().Be($"ocelot:gateway:{gatewayId.Value}");
    }

    [Fact]
    public void Service_ShouldGenerateCorrectKey()
    {
        var serviceId = ServiceId.New();
        var key = RedisKeyHelper.Service(serviceId, Development);
        key.Should().Be($"ocelot:service:development:{serviceId.Value}");
    }

    [Fact]
    public void Route_ShouldGenerateCorrectKey()
    {
        var routeId = RouteId.New();
        var key = RedisKeyHelper.Route(routeId, Development);
        key.Should().Be($"ocelot:route:development:{routeId.Value}");
    }

    [Fact]
    public void Snapshot_ShouldGenerateCorrectKey()
    {
        var version = SnapshotVersion.From(1);
        var key = RedisKeyHelper.Snapshot(version, Development);
        key.Should().Be($"ocelot:snapshot:development:1");
    }

    [Fact]
    public void Publication_ShouldGenerateCorrectKey()
    {
        var publicationId = PublicationId.New();
        var key = RedisKeyHelper.Publication(publicationId);
        key.Should().Be($"ocelot:publication:{publicationId.Value}");
    }

    [Fact]
    public void Plugin_ShouldGenerateCorrectKey()
    {
        var pluginId = PluginId.From("test-plugin");
        var key = RedisKeyHelper.Plugin(pluginId);
        key.Should().Be($"ocelot:plugin:test-plugin");
    }

    [Fact]
    public void RuntimeInstance_ShouldGenerateCorrectKey()
    {
        var gatewayId = GatewayId.New();
        var key = RedisKeyHelper.RuntimeInstance(gatewayId);
        key.Should().Be($"ocelot:runtime:gateway:{gatewayId.Value}");
    }

    [Fact]
    public void License_ShouldGenerateCorrectKey()
    {
        var licenseId = LicenseId.New();
        var key = RedisKeyHelper.License(licenseId);
        key.Should().Be($"ocelot:license:{licenseId.Value}");
    }

    [Fact]
    public void AuditLog_ShouldGenerateCorrectKey()
    {
        var auditId = "audit_123";
        var key = RedisKeyHelper.AuditLog(auditId);
        key.Should().Be($"ocelot:audit:{auditId}");
    }

    [Fact]
    public void IndexKeys_ShouldHaveCorrectValues()
    {
        // The environment-scoped collections carry it too. An index shared between
        // environments is how a delete in one of them orphans the other's row, and how
        // a list answers with a short list that looks like the whole truth.
        RedisKeyHelper.IndexServices(Development).Should().Be("ocelot:index:services:development");
        RedisKeyHelper.IndexRoutes(Development).Should().Be("ocelot:index:routes:development");
        RedisKeyHelper.IndexSnapshots(Development).Should().Be("ocelot:index:snapshots:development");
        RedisKeyHelper.IndexLicenses.Should().Be("ocelot:index:licenses");
        RedisKeyHelper.IndexAuditLogs.Should().Be("ocelot:index:audit-logs");
    }

    [Fact]
    public void IndexServiceRoutes_ShouldGenerateCorrectKey()
    {
        var serviceId = ServiceId.New();
        var key = RedisKeyHelper.IndexServiceRoutes(serviceId, Development);
        key.Should().Be($"ocelot:index:service:development:{serviceId.Value}:routes");
    }

    [Fact]
    public void IndexRouteSignature_ShouldGenerateCorrectKey()
    {
        var signature = "GET:/api/test";
        var key = RedisKeyHelper.IndexRouteSignature(signature, Development);
        key.Should().Be("ocelot:index:route-signature:development:GET:/api/test");
    }

    [Fact]
    public void IndexLicenseProductCodes_ShouldHaveCorrectValue()
    {
        RedisKeyHelper.IndexLicenseProductCodes.Should().Be("ocelot:index:license-product-codes");
    }
}