using BitWrite.OcelotControl.Infrastructure.Redis;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using FluentAssertions;
using Xunit;

namespace BitWrite.OcelotControl.Infrastructure.Tests.Redis;

public class RedisKeyHelperTests
{
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
        var key = RedisKeyHelper.Service(serviceId);
        key.Should().Be($"ocelot:service:{serviceId.Value}");
    }

    [Fact]
    public void Route_ShouldGenerateCorrectKey()
    {
        var routeId = RouteId.New();
        var key = RedisKeyHelper.Route(routeId);
        key.Should().Be($"ocelot:route:{routeId.Value}");
    }

    [Fact]
    public void Snapshot_ShouldGenerateCorrectKey()
    {
        var version = SnapshotVersion.From(1);
        var key = RedisKeyHelper.Snapshot(version);
        key.Should().Be($"ocelot:snapshot:1");
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
        RedisKeyHelper.IndexServices.Should().Be("ocelot:index:services");
        RedisKeyHelper.IndexRoutes.Should().Be("ocelot:index:routes");
        RedisKeyHelper.IndexSnapshots.Should().Be("ocelot:index:snapshots");
        RedisKeyHelper.IndexLicenses.Should().Be("ocelot:index:licenses");
        RedisKeyHelper.IndexAuditLogs.Should().Be("ocelot:index:audit-logs");
    }

    [Fact]
    public void IndexServiceRoutes_ShouldGenerateCorrectKey()
    {
        var serviceId = ServiceId.New();
        var key = RedisKeyHelper.IndexServiceRoutes(serviceId);
        key.Should().Be($"ocelot:index:service:{serviceId.Value}:routes");
    }

    [Fact]
    public void IndexRouteSignature_ShouldGenerateCorrectKey()
    {
        var signature = "GET:/api/test";
        var key = RedisKeyHelper.IndexRouteSignature(signature);
        key.Should().Be("ocelot:index:route-signature:GET:/api/test");
    }

    [Fact]
    public void IndexLicenseProductCodes_ShouldHaveCorrectValue()
    {
        RedisKeyHelper.IndexLicenseProductCodes.Should().Be("ocelot:index:license-product-codes");
    }
}