using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Application.UseCases.Gateway;
using DomainGateway = BitWrite.OcelotControl.Domain.Aggregates.Gateway.Gateway;
using BitWrite.OcelotControl.Domain.Aggregates.Publication;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.Events;
using BitWrite.OcelotControl.Domain.Exceptions;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using FluentAssertions;
using Moq;
using Xunit;

namespace BitWrite.OcelotControl.Application.Tests.UseCases.Gateway;

/// <summary>
/// Gateway deletion, and the publication history that prevents it.
/// </summary>
public class DeleteGatewayCommandHandlerTests
{
    private readonly Mock<IGatewayRepository> _gateways = new();
    private readonly Mock<IPublicationRepository> _publications = new();
    private readonly Mock<IDomainEventDispatcher> _events = new();
    private readonly DeleteGatewayCommandHandler _handler;

    public DeleteGatewayCommandHandlerTests()
    {
        _events.Setup(d => d.DispatchAsync(
                It.IsAny<DomainEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _gateways.Setup(g => g.DeleteAsync(It.IsAny<GatewayId>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _handler = new DeleteGatewayCommandHandler(
            _gateways.Object, _publications.Object, _events.Object);
    }

    private static DomainGateway RegisteredGateway()
    {
        var gateway = DomainGateway.Register("users-gateway", "west coast");
        gateway.ClearDomainEvents();
        return gateway;
    }

    private void Store(DomainGateway gateway) =>
        _gateways.Setup(g => g.GetAsync(gateway.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gateway);

    private void NoPublicationHistory() =>
        _publications.Setup(p => p.HasGatewayBeenPublishedToAsync(
                It.IsAny<GatewayId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

    [Fact]
    public async Task ShouldDelete_GatewayThatWasNeverPublishedTo()
    {
        var gateway = RegisteredGateway();
        Store(gateway);
        NoPublicationHistory();

        var result = await _handler.HandleAsync(
            new DeleteGatewayCommand(gateway.Id, "admin"));

        result.Should().BeTrue();
        _gateways.Verify(g => g.DeleteAsync(gateway.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ShouldRaiseTheDeletionEvent()
    {
        var gateway = RegisteredGateway();
        Store(gateway);
        NoPublicationHistory();

        await _handler.HandleAsync(new DeleteGatewayCommand(gateway.Id, "admin"));

        _events.Verify(d => d.DispatchAsync(
            It.Is<DomainEvent>(e => e is GatewayDeleted), It.IsAny<CancellationToken>()), Times.Once);
        _events.Verify(d => d.DispatchAsync(
            It.Is<DomainEvent>(e => e is AuditRecorded), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ShouldRefuse_GatewayThatHasBeenPublishedTo()
    {
        var gateway = RegisteredGateway();
        Store(gateway);
        _publications.Setup(p => p.HasGatewayBeenPublishedToAsync(
                gateway.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var act = () => _handler.HandleAsync(new DeleteGatewayCommand(gateway.Id, "admin"));

        await act.Should().ThrowAsync<DomainException>();
        // The refusal has to happen before the repository is reached, or the
        // history would be left pointing at something that no longer exists.
        _gateways.Verify(g => g.DeleteAsync(
            It.IsAny<GatewayId>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ShouldReturnFalse_WhenTheGatewayDoesNotExist()
    {
        var id = GatewayId.New();
        _gateways.Setup(g => g.GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DomainGateway?)null);

        var result = await _handler.HandleAsync(new DeleteGatewayCommand(id));

        result.Should().BeFalse();
        _gateways.Verify(g => g.DeleteAsync(
            It.IsAny<GatewayId>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void TheAggregate_ShouldRefuseDeletion_WhenToldItHasHistory()
    {
        // The rule is on the aggregate, so no caller can bypass it by not asking.
        var gateway = RegisteredGateway();

        var act = () => gateway.Delete(hasBeenPublishedTo: true);

        act.Should().Throw<DomainException>()
            .Which.ErrorCode.Should().Be("GATEWAY_HAS_PUBLICATION_HISTORY");
    }

    [Fact]
    public void TheAggregate_ShouldAllowDeletion_WhenItHasNoHistory()
    {
        var gateway = RegisteredGateway();

        gateway.Delete(hasBeenPublishedTo: false);

        gateway.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<GatewayDeleted>();
    }

    [Fact]
    public async Task TheRepository_ShouldAnswerFromEveryPublication()
    {
        // "Ever published to" is the question, not "currently targeted" — a
        // decommissioned gateway still has history pointing at it.
        var gatewayId = GatewayId.New();
        var other = GatewayId.New();

        var repository = new RecordingPublicationRepository(
            publications: new List<Publication>
            {
                Build(targeting: other),
                Build(targeting: gatewayId),
            });

        (await repository.HasGatewayBeenPublishedToAsync(gatewayId)).Should().BeTrue();
        (await repository.HasGatewayBeenPublishedToAsync(GatewayId.New())).Should().BeFalse();
    }

    private static Publication Build(GatewayId targeting) =>
        Publication.Start(SnapshotVersion.First(), "admin", new List<GatewayId> { targeting }, "corr");
}

/// <summary>
/// A stand-in for the Redis repository, so the "ever" semantics are pinned.
/// </summary>
internal class RecordingPublicationRepository : IPublicationRepository
{
    private readonly List<Publication> _publications;

    public RecordingPublicationRepository(List<Publication> publications) =>
        _publications = publications;

    public Task<bool> HasGatewayBeenPublishedToAsync(
        GatewayId gatewayId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_publications.Any(p => p.GatewayStates.ContainsKey(gatewayId)));

    public Task<Publication?> GetAsync(PublicationId id, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<Publication?> GetLatestAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<List<Publication>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_publications);

    public Task AddAsync(Publication publication, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task UpdateAsync(Publication publication, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}
