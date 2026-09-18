using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Application.UseCases.Snapshot;
using BitWrite.OcelotControl.Domain.Aggregates.Snapshot;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;

namespace BitWrite.OcelotControl.Application.UseCases.Snapshot;

public class ExportSnapshotQueryHandler
{
    private readonly ISnapshotRepository _snapshotRepository;

    public ExportSnapshotQueryHandler(ISnapshotRepository snapshotRepository)
    {
        _snapshotRepository = snapshotRepository;
    }

    public async Task<ExportSnapshotResponse?> HandleAsync(ExportSnapshotQuery query, CancellationToken cancellationToken = default)
    {
        var snapshot = await _snapshotRepository.GetAsync(query.Version, cancellationToken);
        if (snapshot == null)
            return null;

        return new ExportSnapshotResponse(
            snapshot.Version,
            snapshot.Content,
            "json"
        );
    }
}