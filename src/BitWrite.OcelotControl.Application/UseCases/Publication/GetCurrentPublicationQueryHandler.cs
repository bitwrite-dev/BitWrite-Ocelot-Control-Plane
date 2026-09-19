using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Application.UseCases.Publication;
using DomainPublication = BitWrite.OcelotControl.Domain.Aggregates.Publication.Publication;
using DomainGatewayDeploymentState = BitWrite.OcelotControl.Domain.Aggregates.Publication.GatewayDeploymentState;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;

namespace BitWrite.OcelotControl.Application.UseCases.Publication;

public class GetCurrentPublicationQueryHandler
{
    private readonly IPublicationRepository _publicationRepository;

    public GetCurrentPublicationQueryHandler(IPublicationRepository publicationRepository)
    {
        _publicationRepository = publicationRepository;
    }

    public async Task<CurrentPublicationResponse> HandleAsync(GetCurrentPublicationQuery query, CancellationToken cancellationToken = default)
    {
        // Get the latest publication
        var latestPublication = await _publicationRepository.GetLatestAsync(cancellationToken);

        // Get all publications for history
        var allPublications = await _publicationRepository.GetAllAsync(cancellationToken);
        var history = allPublications
            .OrderByDescending(p => p.StartedAt)
            .Select(MapToResponse)
            .ToList();

        var current = latestPublication != null ? MapToResponse(latestPublication) : null;

        return new CurrentPublicationResponse(current, history);
    }

    private static PublicationResponse MapToResponse(DomainPublication publication)
    {
        return new PublicationResponse(
            publication.Id,
            publication.SnapshotVersion,
            publication.Status,
            publication.InitiatedBy,
            publication.StartedAt,
            publication.CompletedAt,
            publication.FailureReason,
            publication.ProgressPercentage,
            publication.GatewayStates.Values.Select(MapGatewayState).ToList()
        );
    }

    private static GatewayDeploymentStateResponse MapGatewayState(DomainGatewayDeploymentState state)
    {
        return new GatewayDeploymentStateResponse(
            state.GatewayId,
            state.Status,
            state.ReceivedAt,
            state.ValidatedAt,
            state.AppliedAt,
            state.HealthyAt,
            state.IsValid,
            state.FailedAt,
            state.FailureReason
        );
    }
}