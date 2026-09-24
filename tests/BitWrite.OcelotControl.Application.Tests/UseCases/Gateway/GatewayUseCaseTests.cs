using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Application.UseCases.Gateway;
using DomainGateway = BitWrite.OcelotControl.Domain.Aggregates.Gateway.Gateway;
using BitWrite.OcelotControl.Domain.Events;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Domain.ValueObjects.Status;
using BitWrite.OcelotControl.Domain.Exceptions;
using FluentAssertions;
using Moq;
using Xunit;

namespace BitWrite.OcelotControl.Application.Tests.UseCases.Gateway;

public class RegisterGatewayCommandHandlerTests
{
    private readonly Mock<IGatewayRepository> _mockRepository;
    private readonly Mock<IDomainEventDispatcher> _mockEventDispatcher;
    private readonly RegisterGatewayCommandHandler _handler;

    public RegisterGatewayCommandHandlerTests()
    {
        _mockRepository = new Mock<IGatewayRepository>();
        _mockEventDispatcher = new Mock<IDomainEventDispatcher>();
        _handler = new RegisterGatewayCommandHandler(_mockRepository.Object, _mockEventDispatcher.Object);
    }

    [Fact]
    public async Task HandleAsync_ShouldCreateAndPersistGateway()
    {
        // Arrange
        var command = new RegisterGatewayCommand("Test Gateway", "A test gateway", "test-user");

        _mockRepository.Setup(r => r.AddAsync(It.IsAny<DomainGateway>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockEventDispatcher.Setup(d => d.DispatchAsync(It.Is<DomainEvent>(e => e is DomainEvent), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Test Gateway");
        result.Description.Should().Be("A test gateway");
        result.Status.Should().Be(RuntimeStatus.Disconnected);

        _mockRepository.Verify(r => r.AddAsync(It.IsAny<DomainGateway>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockEventDispatcher.Verify(d => d.DispatchAsync(It.Is<DomainEvent>(e => e is GatewayRegistered), It.IsAny<CancellationToken>()), Times.Once);
        _mockEventDispatcher.Verify(d => d.DispatchAsync(It.Is<DomainEvent>(e => e is AuditRecorded), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ShouldThrowOnEmptyName()
    {
        // Arrange
        var command = new RegisterGatewayCommand("", "A test gateway", "test-user");

        // Act & Assert
        await _handler.Invoking(h => h.HandleAsync(command))
            .Should().ThrowAsync<DomainException>()
            .WithMessage("*empty*");
    }
}

public class GetGatewayQueryHandlerTests
{
    private readonly Mock<IGatewayRepository> _mockRepository;
    private readonly GetGatewayQueryHandler _handler;

    public GetGatewayQueryHandlerTests()
    {
        _mockRepository = new Mock<IGatewayRepository>();
        _handler = new GetGatewayQueryHandler(_mockRepository.Object);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnGateway_WhenExists()
    {
        // Arrange
        var gateway = DomainGateway.Register("Test Gateway", "Description");
        var query = new GetGatewayQuery(gateway.Id);

        _mockRepository.Setup(r => r.GetAsync(query.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gateway);

        // Act
        var result = await _handler.HandleAsync(query);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Value.Should().Be(gateway.Id.Value);
        result.Name.Should().Be("Test Gateway");
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnNull_WhenNotExists()
    {
        // Arrange
        var query = new GetGatewayQuery(GatewayId.New());

        _mockRepository.Setup(r => r.GetAsync(query.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DomainGateway?)null);

        // Act
        var result = await _handler.HandleAsync(query);

        // Assert
        result.Should().BeNull();
    }
}

public class ListGatewaysQueryHandlerTests
{
    private readonly Mock<IGatewayRepository> _mockRepository;
    private readonly ListGatewaysQueryHandler _handler;

    public ListGatewaysQueryHandlerTests()
    {
        _mockRepository = new Mock<IGatewayRepository>();
        _handler = new ListGatewaysQueryHandler(_mockRepository.Object);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnPagedResults()
    {
        // Arrange
        var gateways = new List<DomainGateway>
        {
            DomainGateway.Register("Gateway 1"),
            DomainGateway.Register("Gateway 2"),
            DomainGateway.Register("Gateway 3")
        };

        var query = new ListGatewaysQuery(Page: 1, PageSize: 2);

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(gateways);

        // Act
        var result = await _handler.HandleAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Gateways.Should().HaveCount(2);
        result.TotalCount.Should().Be(3);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(2);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnEmpty_WhenNoGateways()
    {
        // Arrange
        var query = new ListGatewaysQuery();

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DomainGateway>());

        // Act
        var result = await _handler.HandleAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Gateways.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }
}

public class UpdateGatewayCommandHandlerTests
{
    private readonly Mock<IGatewayRepository> _mockRepository;
    private readonly Mock<IDomainEventDispatcher> _mockEventDispatcher;
    private readonly UpdateGatewayCommandHandler _handler;

    public UpdateGatewayCommandHandlerTests()
    {
        _mockRepository = new Mock<IGatewayRepository>();
        _mockEventDispatcher = new Mock<IDomainEventDispatcher>();
        _handler = new UpdateGatewayCommandHandler(_mockRepository.Object, _mockEventDispatcher.Object);
    }

    [Fact]
    public async Task HandleAsync_ShouldUpdateGateway()
    {
        // Arrange
        var gateway = DomainGateway.Register("Original Name", "Original Description");
        var command = new UpdateGatewayCommand(gateway.Id, "Updated Name", "Updated Description", "test-user");

        _mockRepository.Setup(r => r.GetAsync(command.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gateway);
        _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<DomainGateway>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockEventDispatcher.Setup(d => d.DispatchAsync(It.Is<DomainEvent>(e => e is GatewayUpdated), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockEventDispatcher.Setup(d => d.DispatchAsync(It.Is<DomainEvent>(e => e is AuditRecorded), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Updated Name");
        result.Description.Should().Be("Updated Description");

        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<DomainGateway>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockEventDispatcher.Verify(d => d.DispatchAsync(It.Is<DomainEvent>(e => e is GatewayUpdated), It.IsAny<CancellationToken>()), Times.Once);
        _mockEventDispatcher.Verify(d => d.DispatchAsync(It.Is<DomainEvent>(e => e is AuditRecorded), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ShouldThrow_WhenGatewayNotFound()
    {
        // Arrange
        var command = new UpdateGatewayCommand(GatewayId.New(), "Updated Name", null, "test-user");

        _mockRepository.Setup(r => r.GetAsync(command.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DomainGateway?)null);

        // Act & Assert
        await _handler.Invoking(h => h.HandleAsync(command))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }
}

public class UpdateGatewayStatusCommandHandlerTests
{
    private readonly Mock<IGatewayRepository> _mockRepository;
    private readonly Mock<IDomainEventDispatcher> _mockEventDispatcher;
    private readonly UpdateGatewayStatusCommandHandler _handler;

    public UpdateGatewayStatusCommandHandlerTests()
    {
        _mockRepository = new Mock<IGatewayRepository>();
        _mockEventDispatcher = new Mock<IDomainEventDispatcher>();
        _handler = new UpdateGatewayStatusCommandHandler(_mockRepository.Object, _mockEventDispatcher.Object);
    }

    [Fact]
    public async Task HandleAsync_ShouldUpdateStatus()
    {
        // Arrange
        var gateway = DomainGateway.Register("Test Gateway");
        var command = new UpdateGatewayStatusCommand(gateway.Id, RuntimeStatus.Active, "test-user");

        _mockRepository.Setup(r => r.GetAsync(command.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gateway);
        _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<DomainGateway>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockEventDispatcher.Setup(d => d.DispatchAsync(It.Is<DomainEvent>(e => e is GatewayStatusChanged), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockEventDispatcher.Setup(d => d.DispatchAsync(It.Is<DomainEvent>(e => e is AuditRecorded), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(RuntimeStatus.Active);

        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<DomainGateway>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockEventDispatcher.Verify(d => d.DispatchAsync(It.Is<DomainEvent>(e => e is GatewayStatusChanged), It.IsAny<CancellationToken>()), Times.Once);
        _mockEventDispatcher.Verify(d => d.DispatchAsync(It.Is<DomainEvent>(e => e is AuditRecorded), It.IsAny<CancellationToken>()), Times.Once);
    }
}