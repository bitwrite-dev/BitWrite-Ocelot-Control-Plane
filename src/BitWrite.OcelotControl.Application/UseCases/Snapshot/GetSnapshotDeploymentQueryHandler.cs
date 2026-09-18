using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Application.UseCases.Snapshot;
using BitWrite.OcelotControl.Domain.Aggregates.Publication;
using BitWrite.OcelotControl.Domain.Aggregates.Gateway;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Domain.ValueObjects.Status;

namespace BitWrite.OcelotControl.Application.UseCases.Snapshot;

public class GetSnapshotDeploymentQueryHandler
{
    private readonly ISnapshotRepository _snapshotRepository;
    private readonly IPublicationRepository _publicationRepository;
    private readonly IGatewayRepository _gatewayRepository;

    public GetSnapshotDeploymentQueryHandler(
        ISnapshotRepository snapshotRepository,
        IPublicationRepository publicationRepository,
        IGatewayRepository gatewayRepository)
    {
        _snapshotRepository = snapshotRepository;
        _publicationRepository = publicationRepository;
        _gatewayRepository = gatewayRepository;
    }

    public async Task<SnapshotDeploymentResponse?> HandleAsync(GetSnapshotDeploymentQuery query, CancellationToken cancellationToken = default)
    {
        var snapshot = await _snapshotRepository.GetAsync(query.Version, cancellationToken);
        if (snapshot == null)
            return null;

        // Find publication for this snapshot version
        var publications = await _publicationRepository.GetAllAsync(cancellationToken);
        var publication = publications.FirstOrDefault(p => p.SnapshotVersion == query.Version);

        if (publication == null)
        {
            return new SnapshotDeploymentResponse(
                string.Empty,
                query.Version,
                "NotPublished",
                DateTimeOffset.UtcNow,
                null,
                "Snapshot has not been published yet",
                new List<GatewayDeploymentState>()
            );
        }

        // Get gateway states
        var gatewayStates = new List<GatewayDeploymentState>();
        var allGateways = await _gatewayRepository.GetAllAsync(cancellationToken);

        foreach (var gateway in allGateways)
        {
            // In a real implementation, this would check the actual deployment status per gateway
            // For now, we'll use the publication's overall status
            gatewayStates.Add(new GatewayDeploymentState(
                gateway.Id,
                gateway.Name,
                publication.Status.Value,
                publication.StartedAt,
                publication.CompletedAt,
                publication.FailureReason
            ));
        }

        return new SnapshotDeploymentResponse(
            publication.Id.Value.ToString(),
            query.Version,
            publication.Status.Value,
            publication.StartedAt,
            publication.CompletedAt,
            publication.FailureReason,
            gatewayStates
        );
    }
}