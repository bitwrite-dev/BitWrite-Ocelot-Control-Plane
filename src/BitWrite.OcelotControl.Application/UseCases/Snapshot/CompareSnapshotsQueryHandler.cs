using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Application.UseCases.Snapshot;
using BitWrite.OcelotControl.Domain.Aggregates.Snapshot;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;

namespace BitWrite.OcelotControl.Application.UseCases.Snapshot;

public class CompareSnapshotsQueryHandler
{
    private readonly ISnapshotRepository _snapshotRepository;

    public CompareSnapshotsQueryHandler(ISnapshotRepository snapshotRepository)
    {
        _snapshotRepository = snapshotRepository;
    }

    public async Task<CompareSnapshotsResponse?> HandleAsync(CompareSnapshotsQuery query, CancellationToken cancellationToken = default)
    {
        var snapshotA = await _snapshotRepository.GetAsync(query.VersionA, cancellationToken);
        var snapshotB = await _snapshotRepository.GetAsync(query.VersionB, cancellationToken);

        if (snapshotA == null || snapshotB == null)
            return null;

        var differences = new List<string>();

        if (snapshotA.Content != snapshotB.Content)
        {
            differences.Add("Content differs");
        }

        if (snapshotA.Hash != snapshotB.Hash)
        {
            differences.Add($"Hash differs: A={snapshotA.Hash}, B={snapshotB.Hash}");
        }

        if (snapshotA.Status != snapshotB.Status)
        {
            differences.Add($"Status differs: A={snapshotA.Status}, B={snapshotB.Status}");
        }

        if (snapshotA.CreatedBy != snapshotB.CreatedBy)
        {
            differences.Add($"CreatedBy differs: A={snapshotA.CreatedBy}, B={snapshotB.CreatedBy}");
        }

        return new CompareSnapshotsResponse(
            query.VersionA,
            query.VersionB,
            differences
        );
    }
}