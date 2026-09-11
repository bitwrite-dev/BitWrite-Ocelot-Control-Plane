using FluentAssertions;
using BitWrite.OcelotControl.Domain.Aggregates.Gateway;
using BitWrite.OcelotControl.Domain.Aggregates.Route;
using BitWrite.OcelotControl.Domain.Aggregates.Service;
using BitWrite.OcelotControl.Domain.Aggregates.GlobalConfiguration;
using BitWrite.OcelotControl.Domain.Aggregates.Snapshot;
using BitWrite.OcelotControl.Domain.Aggregates.Publication;
using BitWrite.OcelotControl.Domain.Aggregates.Plugin;
using BitWrite.OcelotControl.Domain.Aggregates.RuntimeInstance;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.Status;
using BitWrite.OcelotControl.Domain.ValueObjects.FeatureConfig;
using BitWrite.OcelotControl.Domain.Exceptions;
using BitWrite.OcelotControl.Domain.Events;
using HttpMethod = BitWrite.OcelotControl.Domain.ValueObjects.Configuration.HttpMethod;

namespace BitWrite.OcelotControl.Domain.Tests.Aggregates;

public class GatewayAggregateTests
{
    [Fact]
    public void Register_ShouldCreateGateway()
    {
        var gateway = Gateway.Register("Test Gateway", "A test gateway");

        gateway.Id.Should().NotBeNull();
        gateway.Name.Should().Be("Test Gateway");
        gateway.Description.Should().Be("A test gateway");
        gateway.Status.Should().Be(RuntimeStatus.Disconnected);
    }

    [Fact]
    public void Register_ShouldThrowOnEmptyName()
    {
        var act = () => Gateway.Register("");
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Register_ShouldRaiseDomainEvent()
    {
        var gateway = Gateway.Register("Test Gateway");

        gateway.DomainEvents.Should().HaveCount(1);
        gateway.DomainEvents[0].Should().BeOfType<GatewayRegistered>();
    }

    [Fact]
    public void UpdateMetadata_ShouldUpdate()
    {
        var gateway = Gateway.Register("Test Gateway");

        gateway.UpdateMetadata("environment", "production");

        gateway.Metadata.Should().ContainKey("environment");
        gateway.Metadata["environment"].Should().Be("production");
    }

    [Fact]
    public void RemoveMetadata_ShouldRemove()
    {
        var gateway = Gateway.Register("Test Gateway");
        gateway.UpdateMetadata("environment", "production");

        gateway.RemoveMetadata("environment");

        gateway.Metadata.Should().NotContainKey("environment");
    }

    [Fact]
    public void SetStatus_ShouldUpdateStatus()
    {
        var gateway = Gateway.Register("Test Gateway");

        gateway.SetStatus(RuntimeStatus.Active);

        gateway.Status.Should().Be(RuntimeStatus.Active);
    }

    [Fact]
    public void SetStatus_ShouldRaiseDomainEvent()
    {
        var gateway = Gateway.Register("Test Gateway");

        gateway.SetStatus(RuntimeStatus.Active);

        gateway.DomainEvents.Should().HaveCount(2);
        gateway.DomainEvents[1].Should().BeOfType<GatewayStatusChanged>();
    }

    [Fact]
    public void IsHealthy_ShouldReturnCorrectValue()
    {
        var gateway = Gateway.Register("Test Gateway");

        gateway.IsHealthy.Should().BeFalse();

        gateway.SetStatus(RuntimeStatus.Active);

        gateway.IsHealthy.Should().BeTrue();
    }
}

public class RouteAggregateTests
{
    [Fact]
    public void Create_ShouldCreateRoute()
    {
        var serviceId = ServiceId.New();
        var targets = new List<DownstreamTarget>
        {
            DownstreamTarget.Create("http", "localhost", 5001)
        };

        var route = Route.Create(
            HttpMethod.Get,
            UpstreamPath.From("/api/users"),
            serviceId,
            targets);

        route.Id.Should().NotBeNull();
        route.Method.Should().Be(HttpMethod.Get);
        route.UpstreamPath.Value.Should().Be("/api/users");
        route.ServiceId.Should().Be(serviceId);
        route.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Create_ShouldThrowOnEmptyTargets()
    {
        var act = () => Route.Create(
            HttpMethod.Get,
            UpstreamPath.From("/api/users"),
            ServiceId.New(),
            new List<DownstreamTarget>());

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_ShouldRaiseDomainEvent()
    {
        var route = Route.Create(
            HttpMethod.Get,
            UpstreamPath.From("/api/users"),
            ServiceId.New(),
            new List<DownstreamTarget> { DownstreamTarget.Create("http", "localhost", 5001) });

        route.DomainEvents.Should().HaveCount(1);
        route.DomainEvents[0].Should().BeOfType<RouteCreated>();
    }

    [Fact]
    public void Enable_ShouldEnableRoute()
    {
        var route = CreateTestRoute();
        route.Disable();

        route.Enable();

        route.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Disable_ShouldDisableRoute()
    {
        var route = CreateTestRoute();

        route.Disable();

        route.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void SetAuthentication_ShouldSetOptions()
    {
        var route = CreateTestRoute();
        var options = AuthenticationOptions.Create("Bearer");

        route.SetAuthentication(options);

        route.AuthenticationOptions.Should().Be(options);
    }

    [Fact]
    public void SetRateLimit_ShouldSetOptions()
    {
        var route = CreateTestRoute();
        var options = RateLimitOptions.Create(100, "Minute");

        route.SetRateLimit(options);

        route.RateLimitOptions.Should().Be(options);
    }

    [Fact]
    public void AddDownstreamTarget_ShouldAdd()
    {
        var route = CreateTestRoute();
        var newTarget = DownstreamTarget.Create("http", "localhost", 5002);

        route.AddDownstreamTarget(newTarget);

        route.DownstreamTargets.Should().HaveCount(2);
    }

    [Fact]
    public void AddDownstreamTarget_ShouldThrowOnDuplicate()
    {
        var route = CreateTestRoute();
        var duplicateTarget = DownstreamTarget.Create("http", "localhost", 5001);

        var act = () => route.AddDownstreamTarget(duplicateTarget);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void RemoveDownstreamTarget_ShouldRemove()
    {
        var route = CreateTestRoute();
        route.AddDownstreamTarget(DownstreamTarget.Create("http", "localhost", 5002));

        route.RemoveDownstreamTarget(DownstreamTarget.Create("http", "localhost", 5001));

        route.DownstreamTargets.Should().HaveCount(1);
    }

    [Fact]
    public void RemoveDownstreamTarget_ShouldThrowWhenLastTarget()
    {
        var route = CreateTestRoute();

        var act = () => route.RemoveDownstreamTarget(DownstreamTarget.Create("http", "localhost", 5001));

        act.Should().Throw<DomainException>();
    }

    private Route CreateTestRoute()
    {
        return Route.Create(
            HttpMethod.Get,
            UpstreamPath.From("/api/users"),
            ServiceId.New(),
            new List<DownstreamTarget> { DownstreamTarget.Create("http", "localhost", 5001) });
    }
}

public class ServiceAggregateTests
{
    [Fact]
    public void Create_ShouldCreateService()
    {
        var service = Service.Create("User Service", "Handles user operations");

        service.Id.Should().NotBeNull();
        service.Name.Should().Be("User Service");
        service.Description.Should().Be("Handles user operations");
    }

    [Fact]
    public void Create_ShouldThrowOnEmptyName()
    {
        var act = () => Service.Create("");
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void AddHost_ShouldAddEndpoint()
    {
        var service = Service.Create("User Service");

        service.AddHost("localhost", 5001);

        service.Endpoints.Should().HaveCount(1);
        service.Endpoints[0].Host.Should().Be("localhost");
        service.Endpoints[0].Port.Should().Be(5001);
    }

    [Fact]
    public void AddHost_ShouldThrowOnDuplicate()
    {
        var service = Service.Create("User Service");
        service.AddHost("localhost", 5001);

        var act = () => service.AddHost("localhost", 5001);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void AddHost_ShouldRaiseDomainEvent()
    {
        var service = Service.Create("User Service");

        service.AddHost("localhost", 5001);

        service.DomainEvents.Should().HaveCount(2);
        service.DomainEvents[1].Should().BeOfType<ServiceHostAdded>();
    }

    [Fact]
    public void RemoveHost_ShouldRemoveEndpoint()
    {
        var service = Service.Create("User Service");
        service.AddHost("localhost", 5001);
        service.AddHost("localhost", 5002);

        service.RemoveHost("localhost", 5001);

        service.Endpoints.Should().HaveCount(1);
    }

    [Fact]
    public void RemoveHost_ShouldThrowWhenLastEndpoint()
    {
        var service = Service.Create("User Service");
        service.AddHost("localhost", 5001);

        var act = () => service.RemoveHost("localhost", 5001);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void GetActiveEndpoints_ShouldReturnOnlyActive()
    {
        var service = Service.Create("User Service");
        service.AddHost("localhost", 5001);
        service.AddHost("localhost", 5002);
        service.SetHostActive("localhost", 5002, false);

        var activeEndpoints = service.GetActiveEndpoints();

        activeEndpoints.Should().HaveCount(1);
        activeEndpoints[0].Port.Should().Be(5001);
    }
}

public class GlobalConfigurationAggregateTests
{
    [Fact]
    public void Create_ShouldCreateConfiguration()
    {
        var config = GlobalConfiguration.Create();

        config.Id.Should().NotBe(Guid.Empty);
        config.BaseUrl.Should().Be("http://localhost:5000");
        config.RequestIdKey.Should().Be("X-Request-Id");
    }

    [Fact]
    public void SetBaseUrl_ShouldUpdate()
    {
        var config = GlobalConfiguration.Create();

        config.SetBaseUrl("https://api.example.com");

        config.BaseUrl.Should().Be("https://api.example.com");
    }

    [Fact]
    public void SetDownstreamScheme_ShouldUpdate()
    {
        var config = GlobalConfiguration.Create();

        config.SetDownstreamScheme("https");

        config.DownstreamScheme.Should().Be("https");
    }

    [Fact]
    public void SetDownstreamScheme_ShouldThrowOnInvalid()
    {
        var config = GlobalConfiguration.Create();

        var act = () => config.SetDownstreamScheme("ftp");

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void SetTimeout_ShouldUpdate()
    {
        var config = GlobalConfiguration.Create();

        config.SetTimeout(60000);

        config.Timeout.Should().Be(60000);
    }

    [Fact]
    public void SetTimeout_ShouldThrowOnInvalid()
    {
        var config = GlobalConfiguration.Create();

        var act = () => config.SetTimeout(-1);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void SetServiceDiscovery_ShouldUpdate()
    {
        var config = GlobalConfiguration.Create();
        var discovery = new ServiceDiscoveryConfig
        {
            Provider = "Consul",
            Host = "localhost",
            Port = 8500
        };

        config.SetServiceDiscovery(discovery);

        config.ServiceDiscovery.Should().Be(discovery);
    }
}

public class SnapshotAggregateTests
{
    [Fact]
    public void Create_ShouldCreateSnapshot()
    {
        var content = "test content";
        var hash = ConfigurationHash.FromBytes(new byte[32]);
        var version = SnapshotVersion.From(1);

        var snapshot = Snapshot.Create(content, hash, version, "test-user");

        snapshot.Version.Should().Be(version);
        snapshot.Status.Should().Be(SnapshotStatus.Ready);
        snapshot.Hash.Should().Be(hash);
        snapshot.Content.Should().Be(content);
        snapshot.CreatedBy.Should().Be("test-user");
    }

    [Fact]
    public void Create_ShouldThrowOnEmptyContent()
    {
        var act = () => Snapshot.Create(
            "",
            ConfigurationHash.FromBytes(new byte[32]),
            SnapshotVersion.From(1),
            "test-user");

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Publish_ShouldPublishSnapshot()
    {
        var snapshot = CreateTestSnapshot();

        snapshot.Publish();

        snapshot.Status.Should().Be(SnapshotStatus.Published);
        snapshot.PublishedAt.Should().NotBeNull();
    }

    [Fact]
    public void Archive_ShouldArchiveSnapshot()
    {
        var snapshot = CreateTestSnapshot();
        snapshot.Publish();

        snapshot.Archive();

        snapshot.Status.Should().Be(SnapshotStatus.Archived);
        snapshot.ArchivedAt.Should().NotBeNull();
    }

    [Fact]
    public void VerifyIntegrity_ShouldReturnTrueForValidHash()
    {
        var content = "test content";
        var hash = ConfigurationHash.FromBytes(new byte[32]);
        var snapshot = Snapshot.Create(content, hash, SnapshotVersion.From(1), "test-user");

        snapshot.VerifyIntegrity(hash).Should().BeTrue();
    }

    [Fact]
    public void IsValid_ShouldReturnTrueWhenAllResultsValid()
    {
        var snapshot = CreateTestSnapshot();
        snapshot.AddValidationResult(new ValidationResult { Rule = "Test", IsValid = true });

        snapshot.IsValid.Should().BeTrue();
    }

    [Fact]
    public void IsValid_ShouldReturnFalseWhenAnyResultInvalid()
    {
        var snapshot = CreateTestSnapshot();
        snapshot.AddValidationResult(new ValidationResult { Rule = "Test", IsValid = false });

        snapshot.IsValid.Should().BeFalse();
    }

    private Snapshot CreateTestSnapshot()
    {
        return Snapshot.Create(
            "test content",
            ConfigurationHash.FromBytes(new byte[32]),
            SnapshotVersion.From(1),
            "test-user");
    }
}

public class PublicationAggregateTests
{
    [Fact]
    public void Start_ShouldCreatePublication()
    {
        var version = SnapshotVersion.From(1);
        var gateways = new List<GatewayId> { GatewayId.New(), GatewayId.New() };

        var publication = Publication.Start(version, "admin", gateways);

        publication.Id.Should().NotBeNull();
        publication.SnapshotVersion.Should().Be(version);
        publication.Status.Should().Be(PublicationStatus.Pending);
        publication.InitiatedBy.Should().Be("admin");
        publication.GatewayStates.Should().HaveCount(2);
    }

    [Fact]
    public void Start_ShouldThrowOnEmptyInitiator()
    {
        var act = () => Publication.Start(
            SnapshotVersion.From(1),
            "",
            new List<GatewayId> { GatewayId.New() });

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Start_ShouldThrowOnNoGateways()
    {
        var act = () => Publication.Start(
            SnapshotVersion.From(1),
            "admin",
            new List<GatewayId>());

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void RecordGatewayReceived_ShouldUpdateState()
    {
        var gatewayId = GatewayId.New();
        var publication = Publication.Start(
            SnapshotVersion.From(1),
            "admin",
            new List<GatewayId> { gatewayId });

        publication.RecordGatewayReceived(gatewayId);

        publication.GatewayStates[gatewayId].Status.Should().Be("Received");
    }

    [Fact]
    public void RecordGatewayHealthy_ShouldCompletePublication()
    {
        var gatewayId = GatewayId.New();
        var publication = Publication.Start(
            SnapshotVersion.From(1),
            "admin",
            new List<GatewayId> { gatewayId });

        publication.RecordGatewayHealthy(gatewayId);

        publication.Status.Should().Be(PublicationStatus.Published);
        publication.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void RecordGatewayFailed_ShouldFailPublication()
    {
        var gatewayId = GatewayId.New();
        var publication = Publication.Start(
            SnapshotVersion.From(1),
            "admin",
            new List<GatewayId> { gatewayId });

        publication.RecordGatewayFailed(gatewayId, "Config validation failed");

        publication.Status.Should().Be(PublicationStatus.Failed);
        publication.FailureReason.Should().Contain("Config validation failed");
    }

    [Fact]
    public void Rollback_ShouldRollbackPublication()
    {
        var publication = Publication.Start(
            SnapshotVersion.From(1),
            "admin",
            new List<GatewayId> { GatewayId.New() });

        publication.Rollback(SnapshotVersion.From(1), "Rolling back due to issues");

        publication.Status.Should().Be(PublicationStatus.RolledBack);
    }

    [Fact]
    public void ProgressPercentage_ShouldCalculateCorrectly()
    {
        var gateway1 = GatewayId.New();
        var gateway2 = GatewayId.New();
        var publication = Publication.Start(
            SnapshotVersion.From(1),
            "admin",
            new List<GatewayId> { gateway1, gateway2 });

        publication.RecordGatewayHealthy(gateway1);

        publication.ProgressPercentage.Should().Be(50);
    }
}

public class PluginAggregateTests
{
    [Fact]
    public void Install_ShouldCreatePlugin()
    {
        var plugin = Plugin.Install("my-plugin", "My Plugin", "1.0.0");

        plugin.Id.Value.Should().Be("my-plugin");
        plugin.Name.Should().Be("My Plugin");
        plugin.Version.Should().Be("1.0.0");
        plugin.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Install_ShouldThrowOnEmptyId()
    {
        var act = () => Plugin.Install("", "My Plugin", "1.0.0");
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Enable_ShouldEnablePlugin()
    {
        var plugin = Plugin.Install("my-plugin", "My Plugin", "1.0.0");
        plugin.Disable();

        plugin.Enable();

        plugin.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Disable_ShouldDisablePlugin()
    {
        var plugin = Plugin.Install("my-plugin", "My Plugin", "1.0.0");

        plugin.Disable();

        plugin.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Upgrade_ShouldUpdateVersion()
    {
        var plugin = Plugin.Install("my-plugin", "My Plugin", "1.0.0");

        plugin.Upgrade("2.0.0");

        plugin.Version.Should().Be("2.0.0");
    }

    [Fact]
    public void AddCapability_ShouldAdd()
    {
        var plugin = Plugin.Install("my-plugin", "My Plugin", "1.0.0");
        var capability = new PluginCapability
        {
            Key = "custom-auth",
            Name = "Custom Authentication"
        };

        plugin.AddCapability(capability);

        plugin.HasCapability("custom-auth").Should().BeTrue();
    }

    [Fact]
    public void AddCapability_ShouldThrowOnDuplicate()
    {
        var plugin = Plugin.Install("my-plugin", "My Plugin", "1.0.0");
        var capability = new PluginCapability
        {
            Key = "custom-auth",
            Name = "Custom Authentication"
        };
        plugin.AddCapability(capability);

        var act = () => plugin.AddCapability(capability);

        act.Should().Throw<DomainException>();
    }
}

public class RuntimeInstanceAggregateTests
{
    [Fact]
    public void Register_ShouldCreateInstance()
    {
        var gatewayId = GatewayId.New();
        var capabilities = new List<string> { "http", "https" };

        var instance = RuntimeInstance.Register(gatewayId, capabilities);

        instance.GatewayId.Should().Be(gatewayId);
        instance.Status.Should().Be(RuntimeStatus.Connecting);
        instance.Capabilities.Should().BeEquivalentTo(capabilities);
    }

    [Fact]
    public void Register_ShouldThrowOnNoCapabilities()
    {
        var act = () => RuntimeInstance.Register(GatewayId.New(), new List<string>());
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void RecordHeartbeat_ShouldUpdateTimestamp()
    {
        var instance = CreateTestInstance();

        instance.RecordHeartbeat();

        instance.LastHeartbeat.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void MarkSynchronized_ShouldUpdateStatus()
    {
        var instance = CreateTestInstance();
        var version = SnapshotVersion.From(1);

        instance.MarkSynchronized(version);

        instance.Status.Should().Be(RuntimeStatus.Synchronized);
        instance.CurrentVersion.Should().Be(version);
    }

    [Fact]
    public void MarkActive_ShouldUpdateStatus()
    {
        var instance = CreateTestInstance();

        instance.MarkActive();

        instance.Status.Should().Be(RuntimeStatus.Active);
    }

    [Fact]
    public void MarkDegraded_ShouldUpdateStatus()
    {
        var instance = CreateTestInstance();

        instance.MarkDegraded("High memory usage");

        instance.Status.Should().Be(RuntimeStatus.Degraded);
        instance.RuntimeInfo["DegradedReason"].Should().Be("High memory usage");
    }

    [Fact]
    public void IsHealthy_ShouldReturnCorrectValue()
    {
        var instance = CreateTestInstance();

        instance.IsHealthy.Should().BeFalse();

        instance.MarkActive();

        instance.IsHealthy.Should().BeTrue();
    }

    [Fact]
    public void IsHeartbeatStale_ShouldReturnCorrectValue()
    {
        var instance = CreateTestInstance();
        instance.RecordHeartbeat();

        // Just recorded heartbeat should not be stale for 5 minutes
        instance.IsHeartbeatStale(TimeSpan.FromMinutes(5)).Should().BeFalse();

        // But should be stale for a very small threshold (we use 0 to ensure it's "stale")
        instance.IsHeartbeatStale(TimeSpan.Zero).Should().BeTrue();
    }

    private RuntimeInstance CreateTestInstance()
    {
        return RuntimeInstance.Register(
            GatewayId.New(),
            new List<string> { "http" });
    }
}