using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Application.UseCases.License;
using DomainLicense = BitWrite.OcelotControl.Domain.Aggregates.License.License;
using BitWrite.OcelotControl.Domain.Events;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Domain.ValueObjects.Status;
using BitWrite.OcelotControl.Domain.Exceptions;
using FluentAssertions;
using Moq;
using Xunit;

namespace BitWrite.OcelotControl.Application.Tests.UseCases.License;

public class CreateLicenseCommandHandlerTests
{
    private readonly Mock<ILicenseRepository> _mockRepository;
    private readonly Mock<IDomainEventDispatcher> _mockEventDispatcher;
    private readonly CreateLicenseCommandHandler _handler;

    public CreateLicenseCommandHandlerTests()
    {
        _mockRepository = new Mock<ILicenseRepository>();
        _mockEventDispatcher = new Mock<IDomainEventDispatcher>();
        _handler = new CreateLicenseCommandHandler(_mockRepository.Object, _mockEventDispatcher.Object);
    }

    [Fact]
    public async Task HandleAsync_ShouldCreateLicense()
    {
        // Arrange
        var command = new CreateLicenseCommand(
            "Enterprise License",
            "PROD-ENT-001",
            DateTimeOffset.UtcNow.AddYears(1),
            10,
            100,
            "test-user");

        _mockRepository.Setup(r => r.GetByProductCodeAsync(command.ProductCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DomainLicense?)null);
        _mockRepository.Setup(r => r.AddAsync(It.IsAny<DomainLicense>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockEventDispatcher.Setup(d => d.DispatchAsync(It.Is<DomainEvent>(e => e is LicenseCreated), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockEventDispatcher.Setup(d => d.DispatchAsync(It.Is<DomainEvent>(e => e is AuditRecorded), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Enterprise License");
        result.ProductCode.Should().Be("PROD-ENT-001");
        result.Status.Should().Be(LicenseStatus.Pending);
        result.MaxGateways.Should().Be(10);
        result.MaxRoutes.Should().Be(100);

        _mockRepository.Verify(r => r.AddAsync(It.IsAny<DomainLicense>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockEventDispatcher.Verify(d => d.DispatchAsync(It.Is<DomainEvent>(e => e is LicenseCreated), It.IsAny<CancellationToken>()), Times.Once);
        _mockEventDispatcher.Verify(d => d.DispatchAsync(It.Is<DomainEvent>(e => e is AuditRecorded), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ShouldThrow_WhenProductCodeExists()
    {
        // Arrange
        var existingLicense = DomainLicense.Create("Existing", "PROD-ENT-001", DateTimeOffset.UtcNow.AddYears(1));
        var command = new CreateLicenseCommand("New License", "PROD-ENT-001", DateTimeOffset.UtcNow.AddYears(1));

        _mockRepository.Setup(r => r.GetByProductCodeAsync(command.ProductCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingLicense);

        // Act & Assert
        await _handler.Invoking(h => h.HandleAsync(command))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }
}

public class GetLicenseQueryHandlerTests
{
    private readonly Mock<ILicenseRepository> _mockRepository;
    private readonly GetLicenseQueryHandler _handler;

    public GetLicenseQueryHandlerTests()
    {
        _mockRepository = new Mock<ILicenseRepository>();
        _handler = new GetLicenseQueryHandler(_mockRepository.Object);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnLicense_WhenExists()
    {
        // Arrange
        var license = DomainLicense.Create("Test License", "TEST-001", DateTimeOffset.UtcNow.AddYears(1));
        var query = new GetLicenseQuery(license.Id);

        _mockRepository.Setup(r => r.GetAsync(query.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(license);

        // Act
        var result = await _handler.HandleAsync(query);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Value.Should().Be(license.Id.Value);
        result.Name.Should().Be("Test License");
    }
}

public class ListLicensesQueryHandlerTests
{
    private readonly Mock<ILicenseRepository> _mockRepository;
    private readonly ListLicensesQueryHandler _handler;

    public ListLicensesQueryHandlerTests()
    {
        _mockRepository = new Mock<ILicenseRepository>();
        _handler = new ListLicensesQueryHandler(_mockRepository.Object);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnAllLicenses()
    {
        // Arrange
        var licenses = new List<DomainLicense>
        {
            DomainLicense.Create("License 1", "LIC-001", DateTimeOffset.UtcNow.AddYears(1)),
            DomainLicense.Create("License 2", "LIC-002", DateTimeOffset.UtcNow.AddYears(1)),
            DomainLicense.Create("License 3", "LIC-003", DateTimeOffset.UtcNow.AddYears(1))
        };

        var query = new ListLicensesQuery(Page: 1, PageSize: 2);

        _mockRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(licenses);

        // Act
        var result = await _handler.HandleAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Licenses.Should().HaveCount(2);
        result.TotalCount.Should().Be(3);
    }
}

public class ActivateLicenseCommandHandlerTests
{
    private readonly Mock<ILicenseRepository> _mockRepository;
    private readonly Mock<IDomainEventDispatcher> _mockEventDispatcher;
    private readonly ActivateLicenseCommandHandler _handler;

    public ActivateLicenseCommandHandlerTests()
    {
        _mockRepository = new Mock<ILicenseRepository>();
        _mockEventDispatcher = new Mock<IDomainEventDispatcher>();
        _handler = new ActivateLicenseCommandHandler(_mockRepository.Object, _mockEventDispatcher.Object);
    }

    [Fact]
    public async Task HandleAsync_ShouldActivateLicense_WhenExists()
    {
        // Arrange
        var license = DomainLicense.Create("Test License", "TEST-001", DateTimeOffset.UtcNow.AddYears(1));
        var command = new ActivateLicenseCommand("TEST-001", "test-user");

        _mockRepository.Setup(r => r.GetByProductCodeAsync(command.LicenseKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(license);
        _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<DomainLicense>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockEventDispatcher.Setup(d => d.DispatchAsync(It.Is<DomainEvent>(e => e is LicenseActivated), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockEventDispatcher.Setup(d => d.DispatchAsync(It.Is<DomainEvent>(e => e is AuditRecorded), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.ProductCode.Should().Be("TEST-001");
        result.ActivatedAt.Should().NotBe(default);

        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<DomainLicense>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockEventDispatcher.Verify(d => d.DispatchAsync(It.Is<DomainEvent>(e => e is LicenseActivated), It.IsAny<CancellationToken>()), Times.Once);
        _mockEventDispatcher.Verify(d => d.DispatchAsync(It.Is<DomainEvent>(e => e is AuditRecorded), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnNull_WhenNotFound()
    {
        // Arrange
        var command = new ActivateLicenseCommand("NONEXISTENT", "test-user");

        _mockRepository.Setup(r => r.GetByProductCodeAsync(command.LicenseKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DomainLicense?)null);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.Should().BeNull();
    }
}

public class UpdateLicenseCommandHandlerTests
{
    private readonly Mock<ILicenseRepository> _mockRepository;
    private readonly Mock<IDomainEventDispatcher> _mockEventDispatcher;
    private readonly UpdateLicenseCommandHandler _handler;

    public UpdateLicenseCommandHandlerTests()
    {
        _mockRepository = new Mock<ILicenseRepository>();
        _mockEventDispatcher = new Mock<IDomainEventDispatcher>();
        _handler = new UpdateLicenseCommandHandler(_mockRepository.Object, _mockEventDispatcher.Object);
    }

[Fact]
    public async Task HandleAsync_ShouldUpdateLicense()
    {
        // Arrange
        var license = DomainLicense.Create("Original Name", "TEST-001", DateTimeOffset.UtcNow.AddYears(1));
        license.ClearDomainEvents(); // Clear events from creation
        var command = new UpdateLicenseCommand(license.Id, "Updated Name", "Updated Description", InitiatedBy: "test-user");

        _mockRepository.Setup(r => r.GetAsync(command.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(license);
        _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<DomainLicense>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockEventDispatcher.Setup(d => d.DispatchAsync(It.Is<DomainEvent>(e => e is LicenseUpdated), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockEventDispatcher.Setup(d => d.DispatchAsync(It.Is<DomainEvent>(e => e is AuditRecorded), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Updated Name");

        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<DomainLicense>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockEventDispatcher.Verify(d => d.DispatchAsync(It.Is<DomainEvent>(e => e is LicenseUpdated), It.IsAny<CancellationToken>()), Times.Once);
        _mockEventDispatcher.Verify(d => d.DispatchAsync(It.Is<DomainEvent>(e => e is AuditRecorded), It.IsAny<CancellationToken>()), Times.Once);
    }
}

public class RenewLicenseCommandHandlerTests
{
    private readonly Mock<ILicenseRepository> _mockRepository;
    private readonly Mock<IDomainEventDispatcher> _mockEventDispatcher;
    private readonly RenewLicenseCommandHandler _handler;

    public RenewLicenseCommandHandlerTests()
    {
        _mockRepository = new Mock<ILicenseRepository>();
        _mockEventDispatcher = new Mock<IDomainEventDispatcher>();
        _handler = new RenewLicenseCommandHandler(_mockRepository.Object, _mockEventDispatcher.Object);
    }

    [Fact]
    public async Task HandleAsync_ShouldRenewLicense()
    {
        // Arrange
        var license = DomainLicense.Create("Test License", "TEST-001", DateTimeOffset.UtcNow.AddMonths(6));
        var newExpiration = DateTimeOffset.UtcNow.AddYears(1);
        var command = new RenewLicenseCommand(license.Id, newExpiration, InitiatedBy: "test-user");

        _mockRepository.Setup(r => r.GetAsync(command.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(license);
        _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<DomainLicense>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockEventDispatcher.Setup(d => d.DispatchAsync(It.Is<DomainEvent>(e => e is LicenseRenewed), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockEventDispatcher.Setup(d => d.DispatchAsync(It.Is<DomainEvent>(e => e is AuditRecorded), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.ExpirationDate.Should().Be(newExpiration);

        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<DomainLicense>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockEventDispatcher.Verify(d => d.DispatchAsync(It.Is<DomainEvent>(e => e is LicenseRenewed), It.IsAny<CancellationToken>()), Times.Once);
    }
}

public class RevokeLicenseCommandHandlerTests
{
    private readonly Mock<ILicenseRepository> _mockRepository;
    private readonly Mock<IDomainEventDispatcher> _mockEventDispatcher;
    private readonly RevokeLicenseCommandHandler _handler;

    public RevokeLicenseCommandHandlerTests()
    {
        _mockRepository = new Mock<ILicenseRepository>();
        _mockEventDispatcher = new Mock<IDomainEventDispatcher>();
        _handler = new RevokeLicenseCommandHandler(_mockRepository.Object, _mockEventDispatcher.Object);
    }

    [Fact]
    public async Task HandleAsync_ShouldRevokeLicense()
    {
        // Arrange
        var license = DomainLicense.Create("Test License", "TEST-001", DateTimeOffset.UtcNow.AddYears(1));
        license.Activate();
        var command = new RevokeLicenseCommand(license.Id, "Security violation", "test-user");

        _mockRepository.Setup(r => r.GetAsync(command.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(license);
        _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<DomainLicense>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockEventDispatcher.Setup(d => d.DispatchAsync(It.Is<DomainEvent>(e => e is LicenseRevoked), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockEventDispatcher.Setup(d => d.DispatchAsync(It.Is<DomainEvent>(e => e is AuditRecorded), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(LicenseStatus.Revoked);
        result.RevokedAt.Should().NotBeNull();
        result.RevocationReason.Should().Be("Security violation");

        _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<DomainLicense>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockEventDispatcher.Verify(d => d.DispatchAsync(It.Is<DomainEvent>(e => e is LicenseRevoked), It.IsAny<CancellationToken>()), Times.Once);
    }
}