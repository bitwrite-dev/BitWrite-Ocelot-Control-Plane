using BitWrite.OcelotControl.Domain.Aggregates.Route;
using BitWrite.OcelotControl.Domain.Aggregates.Service;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Infrastructure.Repositories;
using FluentAssertions;
using Moq;
using StackExchange.Redis;
using Xunit;
using HttpMethod = BitWrite.OcelotControl.Domain.ValueObjects.Configuration.HttpMethod;

namespace BitWrite.OcelotControl.Infrastructure.Tests.Repositories;

/// <summary>
/// Round-trip tests for the repositories that rehydrate aggregates.
///
/// The defect these guard against: reads called the domain's Create factory
/// instead of a reconstitution path. Create mints a new id, so reading the same
/// record twice produced two different ids — observed live, where a service
/// returned a different id on every request and creating a route against it
/// failed with "Service not found".
/// </summary>
public class RepositoryRoundTripTests
{
    private readonly Mock<IDatabase> _db = new();
    private readonly Dictionary<string, HashEntry[]> _store = new(StringComparer.Ordinal);

    public RepositoryRoundTripTests()
    {
        var mux = new Mock<IConnectionMultiplexer>();
        mux.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_db.Object);

        _db.Setup(d => d.HashSetAsync(
                It.IsAny<RedisKey>(), It.IsAny<HashEntry[]>(), It.IsAny<CommandFlags>()))
            .Returns<RedisKey, HashEntry[], CommandFlags>((key, entries, _) =>
            {
                _store[key.ToString()!] = entries;
                return Task.CompletedTask;
            });

        _db.Setup(d => d.HashGetAllAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .Returns<RedisKey, CommandFlags>((key, _) =>
                Task.FromResult(_store.TryGetValue(key.ToString()!, out var entries)
                    ? entries
                    : Array.Empty<HashEntry>()));
    }

    private RedisServiceRepository NewServices()
    {
        var mux = new Mock<IConnectionMultiplexer>();
        mux.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_db.Object);
        return new RedisServiceRepository(mux.Object);
    }

    private RedisRouteRepository NewRoutes()
    {
        var mux = new Mock<IConnectionMultiplexer>();
        mux.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_db.Object);
        return new RedisRouteRepository(mux.Object);
    }

    [Fact]
    public async Task Service_ShouldReturnTheSameIdOnEveryRead()
    {
        var repository = NewServices();
        var created = Service.Create("user-service", "the description");
        created.AddHost("host-a", 5001, weight: 3);
        created.AddHost("host-b", 5002);

        await repository.AddAsync(created);

        var first = await repository.GetAsync(created.Id);
        var second = await repository.GetAsync(created.Id);

        first.Should().NotBeNull();
        first!.Id.Should().Be(created.Id);
        second!.Id.Should().Be(created.Id);
    }

    [Fact]
    public async Task Service_ShouldNotSwapNameAndDescription()
    {
        var repository = NewServices();
        var created = Service.Create("user-service", "the description");

        await repository.AddAsync(created);

        var loaded = await repository.GetAsync(created.Id);

        // Create's first parameter is `name`; passing the id there put a UUID in
        // Name and the real name in Description.
        loaded!.Name.Should().Be("user-service");
        loaded.Description.Should().Be("the description");
    }

    [Fact]
    public async Task Service_ShouldPreserveEndpoints()
    {
        var repository = NewServices();
        var created = Service.Create("svc");
        created.AddHost("host-a", 5001, weight: 3);
        created.AddHost("host-b", 5002);

        await repository.AddAsync(created);

        var loaded = await repository.GetAsync(created.Id);

        // ServiceEndpoint has internal setters, so a direct deserialize produced
        // entries with an empty host and port 0.
        loaded!.Endpoints.Should().HaveCount(2);
        loaded.Endpoints[0].Host.Should().Be("host-a");
        loaded.Endpoints[0].Port.Should().Be(5001);
        loaded.Endpoints[0].Weight.Should().Be(3);
        loaded.Endpoints[1].Host.Should().Be("host-b");
    }

    [Fact]
    public async Task Service_ShouldPreserveTimestamps()
    {
        var repository = NewServices();
        var created = Service.Create("svc");

        await repository.AddAsync(created);
        var loaded = await repository.GetAsync(created.Id);

        loaded!.CreatedAt.Should().Be(created.CreatedAt);
        loaded.UpdatedAt.Should().Be(created.UpdatedAt);
    }

    [Fact]
    public async Task Route_ShouldReturnTheSameIdOnEveryRead()
    {
        var repository = NewRoutes();
        var created = Route.Create(
            HttpMethod.Parse("GET"),
            UpstreamPath.From("/api/users"),
            ServiceId.New(),
            new List<DownstreamTarget> { DownstreamTarget.Create("http", "host-a", 5001) },
            key: "user-orders",
            host: "example.com");

        await repository.AddAsync(created);

        var first = await repository.GetAsync(created.Id);
        var second = await repository.GetAsync(created.Id);

        first!.Id.Should().Be(created.Id);
        second!.Id.Should().Be(created.Id);
    }

    [Fact]
    public async Task Route_ShouldPreserveDownstreamTargets()
    {
        var repository = NewRoutes();
        var created = Route.Create(
            HttpMethod.Parse("GET"),
            UpstreamPath.From("/api/users"),
            ServiceId.New(),
            new List<DownstreamTarget>
            {
                DownstreamTarget.Create("http", "host-a", 5001),
                DownstreamTarget.Create("https", "host-b", 5002, "/v2"),
            });

        await repository.AddAsync(created);
        var loaded = await repository.GetAsync(created.Id);

        // DownstreamTarget has a private constructor, so a direct deserialize
        // threw. GetAllAsync swallowed it, emptying the whole route list.
        loaded!.DownstreamTargets.Should().HaveCount(2);
        loaded.DownstreamTargets[0].Host.Should().Be("host-a");
        loaded.DownstreamTargets[1].Scheme.Should().Be("https");
        loaded.DownstreamTargets[1].Port.Should().Be(5002);
        loaded.DownstreamTargets[1].Path.Should().Be("/v2");
    }

    [Fact]
    public async Task Route_ShouldNotDuplicateDownstreamTargets()
    {
        var repository = NewRoutes();
        var created = Route.Create(
            HttpMethod.Parse("GET"),
            UpstreamPath.From("/api/users"),
            ServiceId.New(),
            new List<DownstreamTarget> { DownstreamTarget.Create("http", "host-a", 5001) });

        await repository.AddAsync(created);
        var loaded = await repository.GetAsync(created.Id);

        // The old read called Create (which appends) and then appended again.
        loaded!.DownstreamTargets.Should().HaveCount(1);
    }

    [Fact]
    public async Task Route_ShouldPreserveDisabledState()
    {
        var repository = NewRoutes();
        var created = Route.Create(
            HttpMethod.Parse("GET"),
            UpstreamPath.From("/api/users"),
            ServiceId.New(),
            new List<DownstreamTarget> { DownstreamTarget.Create("http", "host-a", 5001) });
        created.Disable();

        await repository.AddAsync(created);
        var loaded = await repository.GetAsync(created.Id);

        // Create always yields IsEnabled = true, so reloading undid a disable.
        loaded!.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task Route_ShouldPreserveTheFriendlyKeyAndHostSeparately()
    {
        var repository = NewRoutes();
        var created = Route.Create(
            HttpMethod.Parse("GET"),
            UpstreamPath.From("/api/users"),
            ServiceId.New(),
            new List<DownstreamTarget> { DownstreamTarget.Create("http", "host-a", 5001) },
            key: "user-orders",
            host: "example.com");

        await repository.AddAsync(created);
        var loaded = await repository.GetAsync(created.Id);

        // The caller-supplied key was never persisted, so the composite RouteKey
        // signature came back in its place. The old read also passed the host
        // into the key parameter slot.
        loaded!.Key.Should().Be("user-orders");
        loaded.Host.Should().Be("example.com");
    }

    [Fact]
    public async Task Route_ShouldPreserveTimestamps()
    {
        var repository = NewRoutes();
        var created = Route.Create(
            HttpMethod.Parse("GET"),
            UpstreamPath.From("/api/users"),
            ServiceId.New(),
            new List<DownstreamTarget> { DownstreamTarget.Create("http", "host-a", 5001) });

        await repository.AddAsync(created);
        var loaded = await repository.GetAsync(created.Id);

        loaded!.CreatedAt.Should().Be(created.CreatedAt);
        loaded.UpdatedAt.Should().Be(created.UpdatedAt);
    }
}
