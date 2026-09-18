using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Application.UseCases.Snapshot;
using DomainSnapshot = BitWrite.OcelotControl.Domain.Aggregates.Snapshot.Snapshot;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Domain.ValueObjects.Status;

namespace BitWrite.OcelotControl.Application.UseCases.Snapshot;

public class ListSnapshotsQueryHandler
{
    private readonly ISnapshotRepository _snapshotRepository;

    public ListSnapshotsQueryHandler(ISnapshotRepository snapshotRepository)
    {
        _snapshotRepository = snapshotRepository;
    }

    public async Task<SnapshotListResponse> HandleAsync(ListSnapshotsQuery query, CancellationToken cancellationToken = default)
    {
        var snapshots = await _snapshotRepository.GetAllAsync(cancellationToken);

        // Apply status filter if provided
        if (query.Status != null)
        {
            snapshots = snapshots.Where(s => s.Status == query.Status).ToList();
        }

        var totalCount = snapshots.Count;
        var pagedSnapshots = snapshots
            .OrderByDescending(s => s.Version.Value)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();

        var response = new SnapshotListResponse(
            pagedSnapshots.Select(MapToResponse).ToList(),
            totalCount,
            query.Page,
            query.PageSize
        );

        return response;
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