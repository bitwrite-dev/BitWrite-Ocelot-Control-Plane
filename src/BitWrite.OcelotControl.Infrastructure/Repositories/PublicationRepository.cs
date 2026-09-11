using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Domain.Aggregates.Publication;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Domain.ValueObjects.Status;
using BitWrite.OcelotControl.Infrastructure.Redis;
using StackExchange.Redis;

namespace BitWrite.OcelotControl.Infrastructure.Repositories;

public class RedisPublicationRepository : RedisRepositoryBase, IPublicationRepository
{
    public RedisPublicationRepository(IConnectionMultiplexer connectionMultiplexer) 
        : base(connectionMultiplexer)
    {
    }

    public async Task<Publication?> GetAsync(PublicationId id, CancellationToken cancellationToken = default)
    {
        var key = RedisKeyHelper.Publication(id);
        var entries = await GetHashAsync(key);
        
        if (entries.Length == 0)
            return null;

        var publication = Publication.Start(
            SnapshotVersion.From(int.Parse(GetEntry(entries, "SnapshotVersion"))),
            GetEntry(entries, "InitiatedBy"),
            new List<GatewayId>(), // Gateway states are loaded separately
            string.Empty
        );

        var status = GetEntry(entries, "Status");
        if (status == PublicationStatus.Published.Value)
            publication.Complete();
        else if (status == PublicationStatus.Failed.Value)
        {
            publication.RecordGatewayFailed(
                GatewayId.From(GetEntry(entries, "FailureGatewayId")),
                GetEntry(entries, "FailureReason")
            );
        }
        else if (status == PublicationStatus.RolledBack.Value)
        {
            publication.Rollback(
                SnapshotVersion.From(int.Parse(GetEntry(entries, "TargetVersion"))),
                GetEntry(entries, "FailureReason")
            );
        }

        return publication;
    }

    public async Task<Publication?> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        // For now, return null - could be enhanced to track latest
        return null;
    }

    public async Task<List<Publication>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return new List<Publication>();
    }

    public async Task AddAsync(Publication publication, CancellationToken cancellationToken = default)
    {
        var key = RedisKeyHelper.Publication(publication.Id);
        var entries = new HashEntry[]
        {
            new("Id", publication.Id.Value.ToString()),
            new("SnapshotVersion", publication.SnapshotVersion.Value.ToString()),
            new("Status", publication.Status.Value),
            new("InitiatedBy", publication.InitiatedBy),
            new("StartedAt", publication.StartedAt.ToString("O")),
            new("CompletedAt", publication.CompletedAt?.ToString("O") ?? ""),
            new("FailureReason", publication.FailureReason ?? ""),
            new("FailureGatewayId", "") // Would need to track which gateway failed
        };

        await SetHashAsync(key, entries);
    }

    public async Task UpdateAsync(Publication publication, CancellationToken cancellationToken = default)
    {
        await AddAsync(publication, cancellationToken);
    }
}