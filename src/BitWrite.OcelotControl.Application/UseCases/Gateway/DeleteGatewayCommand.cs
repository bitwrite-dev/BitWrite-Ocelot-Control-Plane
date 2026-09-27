using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Domain.Events;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;

namespace BitWrite.OcelotControl.Application.UseCases.Gateway;

/// <summary>
/// Removes a gateway that has never been published to.
/// </summary>
public record DeleteGatewayCommand(
    GatewayId Id,
    string InitiatedBy = "",
    string CorrelationId = ""
);

/// <summary>
/// Deletes the gateway if it has no publication history.
/// </summary>
/// <remarks>
/// Whether it has history is the caller's knowledge, since publications live
/// outside this aggregate. The rule itself is enforced on the aggregate, so
/// passing the wrong value here cannot delete a gateway that should be kept.
/// </remarks>
public class DeleteGatewayCommandHandler
{
    private readonly IGatewayRepository _gatewayRepository;
    private readonly IPublicationRepository _publicationRepository;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public DeleteGatewayCommandHandler(
        IGatewayRepository gatewayRepository,
        IPublicationRepository publicationRepository,
        IDomainEventDispatcher eventDispatcher)
    {
        _gatewayRepository = gatewayRepository;
        _publicationRepository = publicationRepository;
        _eventDispatcher = eventDispatcher;
    }

    public async Task<bool> HandleAsync(
        DeleteGatewayCommand command,
        CancellationToken cancellationToken = default)
    {
        var gateway = await _gatewayRepository.GetAsync(command.Id, cancellationToken);
        if (gateway == null)
            return false;

        var hasBeenPublishedTo = await _publicationRepository.HasGatewayBeenPublishedToAsync(
            command.Id,
            cancellationToken);

        // Throws when the gateway has history, so the repository is never reached.
        gateway.Delete(hasBeenPublishedTo, command.CorrelationId);

        await _gatewayRepository.DeleteAsync(command.Id, cancellationToken);

        foreach (var domainEvent in gateway.DomainEvents)
        {
            await _eventDispatcher.DispatchAsync(domainEvent, cancellationToken);
        }
        gateway.ClearDomainEvents();

        await _eventDispatcher.DispatchAsync(
            new AuditRecorded(
                command.InitiatedBy,
                "DeleteGateway",
                "Gateway",
                command.Id.Value.ToString(),
                "Success"),
            cancellationToken);

        return true;
    }
}
