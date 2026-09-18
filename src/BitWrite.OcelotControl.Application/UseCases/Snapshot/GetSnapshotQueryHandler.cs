using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Application.UseCases.Snapshot;
using DomainSnapshot = BitWrite.OcelotControl.Domain.Aggregates.Snapshot.Snapshot;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Domain.ValueObjects.Status;

namespace BitWrite.OcelotControl.Application.UseCases.Snapshot;

public class GetSnapshotQueryHandler
{
    private readonly ISnapshotRepository _snapshotRepository;

    public GetSnapshotQueryHandler(ISnapshotRepository snapshotRepository)
    {
        _snapshotRepository = snapshotRepository;
    }

    public async Task<SnapshotResponse?> HandleAsync(GetSnapshotQuery query, CancellationToken cancellationToken = default)
    {
        var snapshot = await _snapshotRepository.GetAsync(query.Version, cancellationToken);
        return snapshot != null ? MapToResponse(snapshot) : null;
    }

    private static SnapshotResponse MapToResponse(DomainSnapshot snapshot)
    {
        return new SnapshotResponse(
            snapshot.Version,
            snapshot.Hash,
            snapshot.Content,
            snapshot.Status,
            snapshot.CreatedBy,
            snapshot.CreatedAt,
            snapshot.PublishedAt,
            snapshot.ArchivedAt,
            snapshot.ValidationResults.Select(v => new ValidationResult(v.Rule, v.IsValid, v.Message)).ToList()
        );
    }
}