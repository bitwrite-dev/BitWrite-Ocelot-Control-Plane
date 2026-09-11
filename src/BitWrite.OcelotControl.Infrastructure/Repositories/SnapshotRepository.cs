using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Domain.Aggregates.Snapshot;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Domain.ValueObjects.Status;
using BitWrite.OcelotControl.Infrastructure.Redis;
using StackExchange.Redis;

namespace BitWrite.OcelotControl.Infrastructure.Repositories;

public class RedisSnapshotRepository : RedisRepositoryBase, ISnapshotRepository
{
    public RedisSnapshotRepository(IConnectionMultiplexer connectionMultiplexer) 
        : base(connectionMultiplexer)
    {
    }

    public async Task<Snapshot?> GetAsync(SnapshotVersion version, CancellationToken cancellationToken = default)
    {
        var key = RedisKeyHelper.Snapshot(version);
        var entries = await GetHashAsync(key);
        
        if (entries.Length == 0)
            return null;

        var hash = GetEntry(entries, "Hash");
        var content = GetEntry(entries, "Content");
        var status = GetEntry(entries, "Status");
        var createdBy = GetEntry(entries, "CreatedBy");
        var createdAt = DateTimeOffset.Parse(GetEntry(entries, "CreatedAt"));
        var publishedAt = DateTimeOffset.TryParse(GetEntry(entries, "PublishedAt"), out var pa) ? pa : (DateTimeOffset?)null;
        var archivedAt = DateTimeOffset.TryParse(GetEntry(entries, "ArchivedAt"), out var aa) ? aa : (DateTimeOffset?)null;

        var snapshot = Snapshot.Create(
            content,
            ConfigurationHash.FromString(hash),
            version,
            createdBy
        );

        if (status == SnapshotStatus.Published.Value)
            snapshot.Publish();
        else if (status == SnapshotStatus.Archived.Value)
            snapshot.Archive();

        return snapshot;
    }

    public async Task<Snapshot?> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        var versions = await SortedSetRangeByScoreAsync(RedisKeyHelper.IndexSnapshots, stop: double.PositiveInfinity, take: 1);
        
        if (versions.Length > 0 && int.TryParse(versions[0].ToString(), out var versionInt))
        {
            return await GetAsync(SnapshotVersion.From(versionInt), cancellationToken);
        }

        return null;
    }

    public async Task<List<Snapshot>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var versions = await SortedSetRangeByScoreAsync(RedisKeyHelper.IndexSnapshots, take: 100);
        var snapshots = new List<Snapshot>();

        foreach (var v in versions)
        {
            if (int.TryParse(v.ToString(), out var versionInt))
            {
                var snapshot = await GetAsync(SnapshotVersion.From(versionInt), cancellationToken);
                if (snapshot != null)
                    snapshots.Add(snapshot);
            }
        }

        return snapshots;
    }

    public async Task AddAsync(Snapshot snapshot, CancellationToken cancellationToken = default)
    {
        var key = RedisKeyHelper.Snapshot(snapshot.Version);
        var entries = new HashEntry[]
        {
            new("Version", snapshot.Version.Value.ToString()),
            new("Hash", snapshot.Hash.Value),
            new("Content", snapshot.Content),
            new("Status", snapshot.Status.Value),
            new("CreatedBy", snapshot.CreatedBy),
            new("CreatedAt", snapshot.CreatedAt.ToString("O")),
            new("PublishedAt", snapshot.PublishedAt?.ToString("O") ?? ""),
            new("ArchivedAt", snapshot.ArchivedAt?.ToString("O") ?? "")
        };

        await SetHashAsync(key, entries);
        await SortedSetAddAsync(RedisKeyHelper.IndexSnapshots, snapshot.Version.Value.ToString(), snapshot.Version.Value);
    }

    public async Task UpdateAsync(Snapshot snapshot, CancellationToken cancellationToken = default)
    {
        await AddAsync(snapshot, cancellationToken);
    }
}