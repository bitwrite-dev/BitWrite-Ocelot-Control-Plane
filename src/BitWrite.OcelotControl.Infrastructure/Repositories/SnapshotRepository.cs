using System.Text.Json;
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
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public RedisSnapshotRepository(IConnectionMultiplexer connectionMultiplexer, IEnvironmentContext environmentContext)
        : base(connectionMultiplexer, environmentContext)
    {
    }

    public async Task<Snapshot?> GetAsync(SnapshotVersion version, CancellationToken cancellationToken = default)
    {
        var key = RedisKeyHelper.Snapshot(version, Environment);
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
            document.ArchivedAt,
            document.ValidationResults
                .Select(result => new ValidationResult
                {
                    Rule = result.Rule,
                    IsValid = result.IsValid,
                    Message = result.Message,
                })
                .ToList());
    }

    public async Task<Snapshot?> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        var versions = await SortedSetRangeByScoreAsync(RedisKeyHelper.IndexSnapshots(Environment), stop: double.PositiveInfinity, take: 1);

        if (versions.Length > 0 && int.TryParse(versions[0].ToString(), out var versionInt))
        {
            return await GetAsync(SnapshotVersion.From(versionInt), cancellationToken);
        }

        return null;
    }

    public async Task<List<Snapshot>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var versions = await SortedSetRangeByScoreAsync(RedisKeyHelper.IndexSnapshots(Environment), take: 100);
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
        var key = RedisKeyHelper.Snapshot(snapshot.Version, Environment);
        var document = new SnapshotDocument
        {
            Version = snapshot.Version.Value,
            Hash = snapshot.Hash.Value,
            Content = snapshot.Content,
            Status = snapshot.Status.Value,
            CreatedBy = snapshot.CreatedBy,
            CreatedAt = snapshot.CreatedAt,
            PublishedAt = snapshot.PublishedAt,
            ArchivedAt = snapshot.ArchivedAt,
            // Without this the rules were computed, attached to the aggregate, and
            // lost on the way to storage — so a snapshot read back showed no
            // validation at all, which is what the column has always displayed.
            ValidationResults = snapshot.ValidationResults
                .Select(result => new SnapshotValidationResultDocument
                {
                    Rule = result.Rule,
                    IsValid = result.IsValid,
                    Message = result.Message,
                })
                .ToList()
        };

        await StringSetAsync(key, JsonSerializer.Serialize(document, JsonOptions));
        await SortedSetAddAsync(RedisKeyHelper.IndexSnapshots(Environment), snapshot.Version.Value.ToString(), snapshot.Version.Value);
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
        /// <summary>
        /// The rules run when the snapshot was sealed. Absent on documents written
        /// before this field existed, which deserializes as an empty list.
        /// </summary>
        public List<SnapshotValidationResultDocument> ValidationResults { get; set; } = new();
    }

    private sealed class SnapshotValidationResultDocument
    {
        public string Rule { get; set; } = string.Empty;
        public bool IsValid { get; set; }
        public string? Message { get; set; }
    }
}
