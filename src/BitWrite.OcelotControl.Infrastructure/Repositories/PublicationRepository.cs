using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Domain.Aggregates.Publication;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Domain.ValueObjects.Status;
using BitWrite.OcelotControl.Infrastructure.Redis;
using StackExchange.Redis;

namespace BitWrite.OcelotControl.Infrastructure.Repositories;

public class RedisPublicationRepository : RedisRepositoryBase, IPublicationRepository
{
    private const string PublicationsIndexKey = "ocelot:index:publications";

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
        var latestId = await Database.SortedSetRangeByScoreAsync(
            PublicationsIndexKey, 
            double.NegativeInfinity, 
            double.PositiveInfinity, 
            Exclude.None, 
            Order.Descending, 
            0, 
            1);

        if (latestId.Length == 0)
            return null;

        var id = PublicationId.From(Guid.Parse(latestId[0].ToString()));
        return await GetAsync(id, cancellationToken);
    }

    public async Task<List<Publication>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var publicationIds = await Database.SortedSetRangeByScoreAsync(
            PublicationsIndexKey, 
            double.NegativeInfinity, 
            double.PositiveInfinity, 
            Exclude.None, 
            Order.Descending);

        var publications = new List<Publication>();

        foreach (var id in publicationIds)
        {
            try
            {
                var publicationId = PublicationId.From(Guid.Parse(id.ToString()));
                var publication = await GetAsync(publicationId, cancellationToken);
                if (publication != null)
                    publications.Add(publication);
            }
            catch
            {
                // Skip invalid IDs
            }
        }

        return publications;
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
        await Database.SortedSetAddAsync(PublicationsIndexKey, publication.Id.Value.ToString(), ToUnixTimestamp(publication.StartedAt));
    }

    public async Task UpdateAsync(Publication publication, CancellationToken cancellationToken = default)
    {
        await AddAsync(publication, cancellationToken);
    }

    private static double ToUnixTimestamp(DateTimeOffset dateTime)
    {
        return dateTime.ToUnixTimeSeconds();
    }
}