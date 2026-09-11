using BitWrite.OcelotControl.Domain.Aggregates.Snapshot;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;

namespace BitWrite.OcelotControl.Application.Interfaces;

public interface ISnapshotRepository
{
    Task<Snapshot?> GetAsync(SnapshotVersion version, CancellationToken cancellationToken = default);
    Task<Snapshot?> GetLatestAsync(CancellationToken cancellationToken = default);
    Task<List<Snapshot>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Snapshot snapshot, CancellationToken cancellationToken = default);
    Task UpdateAsync(Snapshot snapshot, CancellationToken cancellationToken = default);
}