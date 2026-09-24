using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Application.UseCases.Service;
using DomainService = BitWrite.OcelotControl.Domain.Aggregates.Service.Service;
using DomainRoute = BitWrite.OcelotControl.Domain.Aggregates.Route.Route;
using DomainHttpMethod = BitWrite.OcelotControl.Domain.ValueObjects.Configuration.HttpMethod;
using DomainUpstreamPath = BitWrite.OcelotControl.Domain.ValueObjects.Configuration.UpstreamPath;
using BitWrite.OcelotControl.Domain.Events;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using FluentAssertions;
using Moq;
using Xunit;

namespace BitWrite.OcelotControl.Application.Tests.UseCases.Service;

public class CreateServiceCommandHandlerTests
{
    private readonly Mock<IServiceRepository> _mockRepository;
    private readonly Mock<IDomainEventDispatcher> _mockEventDispatcher;
    private readonly CreateServiceCommandHandler _handler;

    public CreateServiceCommandHandlerTests()
    {
        _mockRepository = new Mock<IServiceRepository>();
        _mockEventDispatcher = new Mock<IDomainEventDispatcher>();
        _handler = new CreateServiceCommandHandler(_mockRepository.Object, _mockEventDispatcher.Object);
    }

    [Fact]
    public async Task HandleAsync_ShouldCreateService()
    {
        // Arrange
        var command = new CreateServiceCommand("User Service", "Handles user operations", 
            new List<DownstreamTarget> { DownstreamTarget.Create("http", "localhost", 5001) },
            "test-user");

        _mockRepository.Setup(r => r.AddAsync(It.IsAny<DomainService>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockEventDispatcher.Setup(d => d.DispatchAsync(It.Is<DomainEvent>(e => e is DomainEvent), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("User Service");
        result.Description.Should().Be("Handles user operations");
        result.Endpoints.Should().HaveCount(1);

        _mockRepository.Verify(r => r.AddAsync(It.IsAny<DomainService>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockEventDispatcher.Verify(d => d.DispatchAsync(It.Is<DomainEvent>(e => e is ServiceCreated), It.IsAny<CancellationToken>()), Times.Once);
        _mockEventDispatcher.Verify(d => d.DispatchAsync(It.Is<DomainEvent>(e => e is AuditRecorded), It.IsAny<CancellationToken>()), Times.Once);
    }
}

public class GetServiceQueryHandlerTests
{
    private readonly Mock<IServiceRepository> _mockRepository;
    private readonly GetServiceQueryHandler _handler;

    public GetServiceQueryHandlerTests()
    {
        _mockRepository = new Mock<IServiceRepository>();
        _handler = new GetServiceQueryHandler(_mockRepository.Object);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnService_WhenExists()
    {
        // Arrange
        var service = DomainService.Create("User Service", "Handles user operations");
        service.AddHost("localhost", 5001);

        var query = new GetServiceQuery(service.Id);

        _mockRepository.Setup(r => r.GetAsync(query.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(service);

        // Act
        var result = await _handler.HandleAsync(query);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Value.Should().Be(service.Id.Value);
        result.Name.Should().Be("User Service");
    }
}

public class ListServicesQueryHandlerTests
{
    private readonly Mock<IServiceRepository> _mockRepository;
    private readonly ListServicesQueryHandler _handler;

    public ListServicesQueryHandlerTests()
    {
        _mockRepository = new Mock<IServiceRepository>();
        _handler = new ListServicesQueryHandler(_mockRepository.Object);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnPagedResults()
    {
        // Arrange
        var services = new List<DomainService>
        {
            DomainService.Create("Service 1"),
            DomainService.Create("Service 2"),
            DomainService.Create("Service 3")
        };

        var query = new ListServicesQuery(Page: 1, PageSize: 2);

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(services);

        // Act
        var result = await _handler.HandleAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Services.Should().HaveCount(2);
        result.TotalCount.Should().Be(3);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(2);
    }
}

public class UpdateServiceCommandHandlerTests
{
    private readonly Mock<IServiceRepository> _mockRepository;
    private readonly Mock<IDomainEventDispatcher> _mockEventDispatcher;
    private readonly UpdateServiceCommandHandler _handler;

    public UpdateServiceCommandHandlerTests()
    {
        _mockRepository = new Mock<IServiceRepository>();
        _mockEventDispatcher = new Mock<IDomainEventDispatcher>();
        _handler = new UpdateServiceCommandHandler(_mockRepository.Object, _mockEventDispatcher.Object);
    }

    [Fact]
    public async Task HandleAsync_ShouldUpdateService()
    {
        // Arrange
        var service = DomainService.Create("Original Name", "Original Description");
        service.ClearDomainEvents(); // Clear events from creation
        var command = new UpdateServiceCommand(
            service.Id,
            "Updated Name",
            "Updated Description",
            new List<DownstreamTarget> { DownstreamTarget.Create("http", "localhost", 5002) },
            "test-user");

        _mockRepository.Setup(r => r.GetAsync(command.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(service);
        _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<DomainService>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockEventDispatcher.Setup(d => d.DispatchAsync(It.Is<DomainEvent>(e => e is DomainEvent), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Updated Name");
        result.Description.Should().Be("Updated Description");

        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<DomainService>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockEventDispatcher.Verify(d => d.DispatchAsync(It.Is<DomainEvent>(e => e is ServiceUpdated), It.IsAny<CancellationToken>()), Times.Once);
    }
}

public class DeleteServiceCommandHandlerTests
{
    private readonly Mock<IServiceRepository> _mockRepository;
    private readonly Mock<IDomainEventDispatcher> _mockEventDispatcher;
    private readonly DeleteServiceCommandHandler _handler;

    public DeleteServiceCommandHandlerTests()
    {
        _mockRepository = new Mock<IServiceRepository>();
        _mockEventDispatcher = new Mock<IDomainEventDispatcher>();
        _handler = new DeleteServiceCommandHandler(_mockRepository.Object, _mockEventDispatcher.Object);
    }

    [Fact]
    public async Task HandleAsync_ShouldDeleteService_WhenExists()
    {
        // Arrange
        var service = DomainService.Create("Test Service");
        var command = new DeleteServiceCommand(service.Id, "test-user");

        _mockRepository.Setup(r => r.GetAsync(command.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(service);
        _mockRepository.Setup(r => r.DeleteAsync(command.Id, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockEventDispatcher.Setup(d => d.DispatchAsync(It.Is<DomainEvent>(e => e is DomainEvent), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.Should().BeTrue();
        _mockRepository.Verify(r => r.DeleteAsync(command.Id, It.IsAny<CancellationToken>()), Times.Once);
        _mockEventDispatcher.Verify(d => d.DispatchAsync(It.Is<DomainEvent>(e => e is ServiceDeleted), It.IsAny<CancellationToken>()), Times.Once);
        _mockEventDispatcher.Verify(d => d.DispatchAsync(It.Is<DomainEvent>(e => e is AuditRecorded), It.IsAny<CancellationToken>()), Times.Once);
    }
}

public class GetServiceRoutesQueryHandlerTests
{
    private readonly Mock<IServiceRepository> _mockServiceRepository;
    private readonly Mock<IRouteRepository> _mockRouteRepository;
    private readonly GetServiceRoutesQueryHandler _handler;

    public GetServiceRoutesQueryHandlerTests()
    {
        _mockServiceRepository = new Mock<IServiceRepository>();
        _mockRouteRepository = new Mock<IRouteRepository>();
        _handler = new GetServiceRoutesQueryHandler(_mockServiceRepository.Object, _mockRouteRepository.Object);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnRoutes_WhenServiceExists()
    {
        // Arrange
        var serviceId = ServiceId.New();
        var routes = new List<DomainRoute>
        {
            DomainRoute.Create(DomainHttpMethod.Get, DomainUpstreamPath.From("/api/1"), serviceId, new List<DownstreamTarget> { DownstreamTarget.Create("http", "localhost", 5001) }, "route-1"),
            DomainRoute.Create(DomainHttpMethod.Post, DomainUpstreamPath.From("/api/2"), serviceId, new List<DownstreamTarget> { DownstreamTarget.Create("http", "localhost", 5001) }, "route-2")
        };

        var query = new GetServiceRoutesQuery(ServiceId.From(serviceId.Value.ToString()));

        _mockServiceRepository.Setup(r => r.GetAsync(query.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(DomainService.Create("Test Service"));
        _mockRouteRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(routes);

        // Act
        var result = await _handler.HandleAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Routes.Should().HaveCount(2);
    }
}