using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Application.UseCases.Publication;
using DomainPublication = BitWrite.OcelotControl.Domain.Aggregates.Publication.Publication;
using DomainGatewayDeploymentState = BitWrite.OcelotControl.Domain.Aggregates.Publication.GatewayDeploymentState;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;

namespace BitWrite.OcelotControl.Application.UseCases.Publication;

public class ListPublicationsQueryHandler
{
    private readonly IPublicationRepository _publicationRepository;

    public ListPublicationsQueryHandler(IPublicationRepository publicationRepository)
    {
        _publicationRepository = publicationRepository;
    }

    public async Task<PublicationListResponse> HandleAsync(ListPublicationsQuery query, CancellationToken cancellationToken = default)
    {
        var publications = await _publicationRepository.GetAllAsync(cancellationToken);

        var totalCount = publications.Count;
        var pagedPublications = publications
            .OrderByDescending(p => p.StartedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();

        var response = new PublicationListResponse(
            pagedPublications.Select(MapToResponse).ToList(),
            totalCount,
            query.Page,
            query.PageSize
        );

        return response;
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