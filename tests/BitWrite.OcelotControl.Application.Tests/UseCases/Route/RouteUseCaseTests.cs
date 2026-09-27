using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Application.UseCases.Route;
using DomainRoute = BitWrite.OcelotControl.Domain.Aggregates.Route.Route;
using DomainService = BitWrite.OcelotControl.Domain.Aggregates.Service.Service;
using DomainHttpMethod = BitWrite.OcelotControl.Domain.ValueObjects.Configuration.HttpMethod;
using DomainUpstreamPath = BitWrite.OcelotControl.Domain.ValueObjects.Configuration.UpstreamPath;
using BitWrite.OcelotControl.Domain.Events;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.FeatureConfig;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using FluentAssertions;
using Moq;
using Xunit;

namespace BitWrite.OcelotControl.Application.Tests.UseCases.Route;

public class CreateRouteCommandHandlerTests
{
    private readonly Mock<IRouteRepository> _mockRouteRepository;
    private readonly Mock<IServiceRepository> _mockServiceRepository;
    private readonly Mock<IDomainEventDispatcher> _mockEventDispatcher;
    private readonly CreateRouteCommandHandler _handler;

    public CreateRouteCommandHandlerTests()
    {
        _mockRouteRepository = new Mock<IRouteRepository>();
        _mockServiceRepository = new Mock<IServiceRepository>();
        _mockEventDispatcher = new Mock<IDomainEventDispatcher>();
        _handler = new CreateRouteCommandHandler(_mockRouteRepository.Object, _mockServiceRepository.Object, _mockEventDispatcher.Object);
    }

    [Fact]
    public async Task HandleAsync_ShouldCreateRoute_WhenServiceExists()
    {
        // Arrange
        var serviceId = ServiceId.New();
        var service = DomainService.Create("Test Service");
        var targets = new List<DownstreamTarget>
        {
            DownstreamTarget.Create("http", "localhost", 5001)
        };

        var command = new CreateRouteCommand(
            "test-route",
            DomainHttpMethod.Get,
            DomainUpstreamPath.From("/api/test"),
            serviceId,
            targets,
            "localhost",
            null, null, null, null, null,
            "test-user");

        _mockServiceRepository.Setup(r => r.GetAsync(serviceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(service);
        _mockRouteRepository.Setup(r => r.AddAsync(It.IsAny<DomainRoute>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockEventDispatcher.Setup(d => d.DispatchAsync(It.Is<DomainEvent>(e => e is DomainEvent), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.Key.Should().Be("test-route");
        result.Method.Value.Should().Be("GET");
        result.UpstreamPath.Value.Should().Be("/api/test");
        result.ServiceId.Value.Should().Be(serviceId.Value);

        _mockRouteRepository.Verify(r => r.AddAsync(It.IsAny<DomainRoute>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockEventDispatcher.Verify(d => d.DispatchAsync(It.Is<DomainEvent>(e => e is RouteCreated), It.IsAny<CancellationToken>()), Times.Once);
        _mockEventDispatcher.Verify(d => d.DispatchAsync(It.Is<DomainEvent>(e => e is AuditRecorded), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ShouldThrow_WhenServiceNotFound()
    {
        // Arrange
        var serviceId = ServiceId.New();
        var command = new CreateRouteCommand(
            "test-route",
            DomainHttpMethod.Get,
            DomainUpstreamPath.From("/api/test"),
            serviceId,
            new List<DownstreamTarget> { DownstreamTarget.Create("http", "localhost", 5001) },
            null, null, null, null, null, null,
            "test-user");

        _mockServiceRepository.Setup(r => r.GetAsync(serviceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DomainService?)null);

        // Act & Assert
        await _handler.Invoking(h => h.HandleAsync(command))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }
}

public class GetRouteQueryHandlerTests
{
    private readonly Mock<IRouteRepository> _mockRepository;
    private readonly GetRouteQueryHandler _handler;

    public GetRouteQueryHandlerTests()
    {
        _mockRepository = new Mock<IRouteRepository>();
        _handler = new GetRouteQueryHandler(_mockRepository.Object);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnRoute_WhenExists()
    {
        // Arrange
        var serviceId = ServiceId.New();
        var route = DomainRoute.Create(
            DomainHttpMethod.Get,
            DomainUpstreamPath.From("/api/test"),
            serviceId,
            new List<DownstreamTarget> { DownstreamTarget.Create("http", "localhost", 5001) },
            "test-route");

        var query = new GetRouteQuery(route.Id);

        _mockRepository.Setup(r => r.GetAsync(query.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(route);

        // Act
        var result = await _handler.HandleAsync(query);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Value.Should().Be(route.Id.Value);
        result.Key.Should().Be("test-route");
    }
}

public class ListRoutesQueryHandlerTests
{
    private readonly Mock<IRouteRepository> _mockRepository;
    private readonly ListRoutesQueryHandler _handler;

    public ListRoutesQueryHandlerTests()
    {
        _mockRepository = new Mock<IRouteRepository>();
        _handler = new ListRoutesQueryHandler(_mockRepository.Object);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnPagedResults()
    {
        // Arrange
        var serviceId = ServiceId.New();
        var routes = new List<DomainRoute>
        {
            DomainRoute.Create(DomainHttpMethod.Get, DomainUpstreamPath.From("/api/1"), serviceId, new List<DownstreamTarget> { DownstreamTarget.Create("http", "localhost", 5001) }, "route-1"),
            DomainRoute.Create(DomainHttpMethod.Post, DomainUpstreamPath.From("/api/2"), serviceId, new List<DownstreamTarget> { DownstreamTarget.Create("http", "localhost", 5001) }, "route-2"),
            DomainRoute.Create(DomainHttpMethod.Put, DomainUpstreamPath.From("/api/3"), serviceId, new List<DownstreamTarget> { DownstreamTarget.Create("http", "localhost", 5001) }, "route-3")
        };

        var query = new ListRoutesQuery(Page: 1, PageSize: 2);

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(routes);

        // Act
        var result = await _handler.HandleAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Routes.Should().HaveCount(2);
        result.TotalCount.Should().Be(3);
    }
}

public class ReplaceRouteCommandHandlerTests
{
    private readonly Mock<IRouteRepository> _mockRouteRepository;
    private readonly Mock<IServiceRepository> _mockServiceRepository;
    private readonly Mock<IDomainEventDispatcher> _mockEventDispatcher;
    private readonly ReplaceRouteCommandHandler _handler;

    public ReplaceRouteCommandHandlerTests()
    {
        _mockRouteRepository = new Mock<IRouteRepository>();
        _mockServiceRepository = new Mock<IServiceRepository>();
        _mockEventDispatcher = new Mock<IDomainEventDispatcher>();
        _handler = new ReplaceRouteCommandHandler(
            _mockRouteRepository.Object,
            _mockServiceRepository.Object,
            _mockEventDispatcher.Object);
    }

    private static DomainRoute StoredRoute()
    {
        var serviceId = ServiceId.New();
        return DomainRoute.Create(
            DomainHttpMethod.Get,
            DomainUpstreamPath.From("/api/test"),
            serviceId,
            new List<DownstreamTarget> { DownstreamTarget.Create("http", "localhost", 5001) },
            "original-route",
            "api.example.com");
    }

    private void Store(DomainRoute route) =>
        _mockRouteRepository.Setup(r => r.GetAsync(route.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(route);

    private void AllowUpdate() =>
        _mockRouteRepository.Setup(r => r.UpdateAsync(It.IsAny<DomainRoute>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

    [Fact]
    public async Task HandleAsync_ShouldReplaceEveryField()
    {
        // Arrange
        var route = StoredRoute();
        var newService = ServiceId.New();
        var command = new ReplaceRouteCommand(
            route.Id,
            DomainHttpMethod.Post,
            DomainUpstreamPath.From("/api/updated"),
            newService,
            new List<DownstreamTarget> { DownstreamTarget.Create("http", "localhost", 5002) },
            "updated-route",
            "other.example.com",
            null, null, null, null, null,
            "test-user");

        Store(route);
        AllowUpdate();
        _mockServiceRepository.Setup(r => r.GetAsync(newService, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DomainService.Create("new service"));
        _mockEventDispatcher.Setup(d => d.DispatchAsync(It.IsAny<DomainEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.Should().NotBeNull();

        // Each of these was silently dropped before: method, upstream path,
        // downstream targets and the service reference.
        result!.Key.Should().Be("updated-route");
        result.Method.Should().Be(DomainHttpMethod.Post);
        result.UpstreamPath.Should().Be(DomainUpstreamPath.From("/api/updated"));
        result.ServiceId.Should().Be(newService);
        result.DownstreamTargets.Should().ContainSingle()
            .Which.Port.Should().Be(5002);
        route.Host.Should().Be("other.example.com");

        _mockRouteRepository.Verify(r => r.UpdateAsync(route, It.IsAny<CancellationToken>()), Times.Once);
        _mockEventDispatcher.Verify(d => d.DispatchAsync(It.Is<DomainEvent>(e => e is RouteUpdated), It.IsAny<CancellationToken>()), Times.Once);
        _mockEventDispatcher.Verify(d => d.DispatchAsync(It.Is<DomainEvent>(e => e is AuditRecorded), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ShouldClearTheHost_WhenItIsNotSupplied()
    {
        // A null host means "remove it", which was impossible under the old
        // merge semantics.
        var route = StoredRoute();
        var command = new ReplaceRouteCommand(
            route.Id,
            DomainHttpMethod.Get,
            DomainUpstreamPath.From("/api/test"),
            route.ServiceId,
            new List<DownstreamTarget> { DownstreamTarget.Create("http", "localhost", 5001) },
            "original-route",
            Host: null);

        Store(route);
        AllowUpdate();
        _mockEventDispatcher.Setup(d => d.DispatchAsync(It.IsAny<DomainEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _handler.HandleAsync(command);

        route.Host.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_ShouldRemoveAFeatureBlock_WhenItIsNotSupplied()
    {
        // Every option block is supplied on the stored route, and the
        // replacement omits them all.
        var route = DomainRoute.Create(
            DomainHttpMethod.Get,
            DomainUpstreamPath.From("/api/test"),
            ServiceId.New(),
            new List<DownstreamTarget> { DownstreamTarget.Create("http", "localhost", 5001) },
            "key");

        route.SetAuthentication(AuthenticationOptions.Create(
            "Bearer",
            null,
            new Dictionary<string, string> { ["scopes"] = "a,b" }));
        route.SetRateLimit(RateLimitOptions.Create(10, "Minute"));
        route.SetQoS(QoSOptions.Create(30));
        route.SetCache(CacheOptions.Create(60));
        route.SetLoadBalancer(LoadBalancerOptions.Create("RoundRobin"));

        route.AuthenticationOptions.Should().NotBeNull();

        var command = new ReplaceRouteCommand(
            route.Id,
            DomainHttpMethod.Get,
            DomainUpstreamPath.From("/api/test"),
            route.ServiceId,
            new List<DownstreamTarget> { DownstreamTarget.Create("http", "localhost", 5001) },
            "key");

        Store(route);
        AllowUpdate();
        _mockEventDispatcher.Setup(d => d.DispatchAsync(It.IsAny<DomainEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _handler.HandleAsync(command);

        result!.AuthenticationOptions.Should().BeNull();
        result.RateLimitOptions.Should().BeNull();
        result.QoSOptions.Should().BeNull();
        result.CacheOptions.Should().BeNull();
        result.LoadBalancerOptions.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_ShouldNotWrite_WhenTheServiceIsMissing()
    {
        var route = StoredRoute();
        var missing = ServiceId.New();
        var command = new ReplaceRouteCommand(
            route.Id,
            DomainHttpMethod.Get,
            DomainUpstreamPath.From("/api/test"),
            missing,
            new List<DownstreamTarget> { DownstreamTarget.Create("http", "localhost", 5001) });

        Store(route);
        _mockServiceRepository.Setup(r => r.GetAsync(missing, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DomainService?)null);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.HandleAsync(command));

        // The route must be left alone rather than pointed at nothing.
        _mockRouteRepository.Verify(
            r => r.UpdateAsync(It.IsAny<DomainRoute>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnNull_WhenTheRouteDoesNotExist()
    {
        var id = RouteId.New();
        _mockRouteRepository.Setup(r => r.GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DomainRoute?)null);

        var result = await _handler.HandleAsync(new ReplaceRouteCommand(
            id,
            DomainHttpMethod.Get,
            DomainUpstreamPath.From("/api/test"),
            ServiceId.New(),
            new List<DownstreamTarget> { DownstreamTarget.Create("http", "localhost", 5001) }));

        result.Should().BeNull();
        _mockRouteRepository.Verify(
            r => r.UpdateAsync(It.IsAny<DomainRoute>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}

public class EnableRouteCommandHandlerTests
{
    private readonly Mock<IRouteRepository> _mockRepository;
    private readonly Mock<IDomainEventDispatcher> _mockEventDispatcher;
    private readonly EnableRouteCommandHandler _handler;

    public EnableRouteCommandHandlerTests()
    {
        _mockRepository = new Mock<IRouteRepository>();
        _mockEventDispatcher = new Mock<IDomainEventDispatcher>();
        _handler = new EnableRouteCommandHandler(_mockRepository.Object, _mockEventDispatcher.Object);
    }

    [Fact]
    public async Task HandleAsync_ShouldEnableRoute()
    {
        // Arrange
        var serviceId = ServiceId.New();
        var route = DomainRoute.Create(
            DomainHttpMethod.Get,
            DomainUpstreamPath.From("/api/test"),
            serviceId,
            new List<DownstreamTarget> { DownstreamTarget.Create("http", "localhost", 5001) },
            "test-route");
        route.Disable();

        var command = new EnableRouteCommand(route.Id, "test-user");

        _mockRepository.Setup(r => r.GetAsync(route.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(route);
        _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<DomainRoute>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockEventDispatcher.Setup(d => d.DispatchAsync(It.Is<DomainEvent>(e => e is RouteEnabled), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockEventDispatcher.Setup(d => d.DispatchAsync(It.Is<DomainEvent>(e => e is AuditRecorded), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.IsEnabled.Should().BeTrue();

        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<DomainRoute>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockEventDispatcher.Verify(d => d.DispatchAsync(It.Is<DomainEvent>(e => e is RouteEnabled), It.IsAny<CancellationToken>()), Times.Once);
        _mockEventDispatcher.Verify(d => d.DispatchAsync(It.Is<DomainEvent>(e => e is AuditRecorded), It.IsAny<CancellationToken>()), Times.Once);
    }
}

public class DisableRouteCommandHandlerTests
{
    private readonly Mock<IRouteRepository> _mockRepository;
    private readonly Mock<IDomainEventDispatcher> _mockEventDispatcher;
    private readonly DisableRouteCommandHandler _handler;

    public DisableRouteCommandHandlerTests()
    {
        _mockRepository = new Mock<IRouteRepository>();
        _mockEventDispatcher = new Mock<IDomainEventDispatcher>();
        _handler = new DisableRouteCommandHandler(_mockRepository.Object, _mockEventDispatcher.Object);
    }

    [Fact]
    public async Task HandleAsync_ShouldDisableRoute()
    {
        // Arrange
        var serviceId = ServiceId.New();
        var route = DomainRoute.Create(
            DomainHttpMethod.Get,
            DomainUpstreamPath.From("/api/test"),
            serviceId,
            new List<DownstreamTarget> { DownstreamTarget.Create("http", "localhost", 5001) },
            "test-route");

        var command = new DisableRouteCommand(route.Id, "test-user");

        _mockRepository.Setup(r => r.GetAsync(route.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(route);
        _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<DomainRoute>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockEventDispatcher.Setup(d => d.DispatchAsync(It.Is<DomainEvent>(e => e is RouteDisabled), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockEventDispatcher.Setup(d => d.DispatchAsync(It.Is<DomainEvent>(e => e is AuditRecorded), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.IsEnabled.Should().BeFalse();

        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<DomainRoute>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockEventDispatcher.Verify(d => d.DispatchAsync(It.Is<DomainEvent>(e => e is RouteDisabled), It.IsAny<CancellationToken>()), Times.Once);
    }
}

public class DeleteRouteCommandHandlerTests
{
    private readonly Mock<IRouteRepository> _mockRepository;
    private readonly Mock<IDomainEventDispatcher> _mockEventDispatcher;
    private readonly DeleteRouteCommandHandler _handler;

    public DeleteRouteCommandHandlerTests()
    {
        _mockRepository = new Mock<IRouteRepository>();
        _mockEventDispatcher = new Mock<IDomainEventDispatcher>();
        _handler = new DeleteRouteCommandHandler(_mockRepository.Object, _mockEventDispatcher.Object);
    }

    [Fact]
    public async Task HandleAsync_ShouldDeleteRoute_WhenExists()
    {
        // Arrange
        var serviceId = ServiceId.New();
        var route = DomainRoute.Create(
            DomainHttpMethod.Get,
            DomainUpstreamPath.From("/api/test"),
            serviceId,
            new List<DownstreamTarget> { DownstreamTarget.Create("http", "localhost", 5001) },
            "test-route");

        var command = new DeleteRouteCommand(route.Id, "test-user");

        _mockRepository.Setup(r => r.GetAsync(route.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(route);
        _mockRepository.Setup(r => r.DeleteAsync(route.Id, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockEventDispatcher.Setup(d => d.DispatchAsync(It.Is<DomainEvent>(e => e is RouteDeleted), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockEventDispatcher.Setup(d => d.DispatchAsync(It.Is<DomainEvent>(e => e is AuditRecorded), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.Should().BeTrue();
        _mockRepository.Verify(r => r.DeleteAsync(route.Id, It.IsAny<CancellationToken>()), Times.Once);
        _mockEventDispatcher.Verify(d => d.DispatchAsync(It.Is<DomainEvent>(e => e is RouteDeleted), It.IsAny<CancellationToken>()), Times.Once);
        _mockEventDispatcher.Verify(d => d.DispatchAsync(It.Is<DomainEvent>(e => e is AuditRecorded), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnFalse_WhenNotExists()
    {
        // Arrange
        var command = new DeleteRouteCommand(RouteId.New(), "test-user");

        _mockRepository.Setup(r => r.GetAsync(command.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DomainRoute?)null);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.Should().BeFalse();
    }
}