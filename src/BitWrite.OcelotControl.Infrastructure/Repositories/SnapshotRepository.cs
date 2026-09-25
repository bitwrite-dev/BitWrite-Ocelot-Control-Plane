using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Domain.Aggregates.Snapshot;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Domain.ValueObjects.Status;
using BitWrite.OcelotControl.Infrastructure.Redis;
using StackExchange.Redis;
using System.Text.Json;

namespace BitWrite.OcelotControl.Infrastructure.Repositories;

public class RedisSnapshotRepository : RedisRepositoryBase, ISnapshotRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public RedisSnapshotRepository(IConnectionMultiplexer connectionMultiplexer) 
        : base(connectionMultiplexer)
    {
    }

    public async Task<Snapshot?> GetAsync(SnapshotVersion version, CancellationToken cancellationToken = default)
    {
        var key = RedisKeyHelper.Snapshot(version);
        var json = await StringGetAsync(key);

        if (string.IsNullOrWhiteSpace(json))
            return null;

        var document = JsonSerializer.Deserialize<SnapshotDocument>(json, JsonOptions);
        if (document == null)
            return null;

        return Snapshot.Reconstitute(
            document.Content,
            ConfigurationHash.FromString(document.Hash),
            SnapshotVersion.From(document.Version),
            SnapshotStatus.From(document.Status),
            document.CreatedBy,
            document.CreatedAt,
            document.PublishedAt,
            document.ArchivedAt);
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
        var document = new SnapshotDocument
        {
            Version = snapshot.Version.Value,
            Hash = snapshot.Hash.Value,
            Content = snapshot.Content,
            Status = snapshot.Status.Value,
            CreatedBy = snapshot.CreatedBy,
            CreatedAt = snapshot.CreatedAt,
            PublishedAt = snapshot.PublishedAt,
            ArchivedAt = snapshot.ArchivedAt
        };

        await StringSetAsync(key, JsonSerializer.Serialize(document, JsonOptions));
        await SortedSetAddAsync(RedisKeyHelper.IndexSnapshots, snapshot.Version.Value.ToString(), snapshot.Version.Value);
    }

    public async Task UpdateAsync(Snapshot snapshot, CancellationToken cancellationToken = default)
    {
        await AddAsync(snapshot, cancellationToken);
    }

    private sealed class SnapshotDocument
    {
        public int Version { get; set; }
        public string Hash { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? PublishedAt { get; set; }
        public DateTimeOffset? ArchivedAt { get; set; }
    }
}