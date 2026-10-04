using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Domain.Aggregates.Publication;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Domain.ValueObjects.Status;
using BitWrite.OcelotControl.Infrastructure.Redis;
using StackExchange.Redis;
using System.Globalization;

namespace BitWrite.OcelotControl.Infrastructure.Repositories;

public class RedisPublicationRepository : RedisRepositoryBase, IPublicationRepository
{
    private const string PublicationsIndexKey = "ocelot:index:publications";

    public RedisPublicationRepository(IConnectionMultiplexer connectionMultiplexer, IEnvironmentContext environmentContext) 
        : base(connectionMultiplexer, environmentContext)
    {
    }

    public async Task<Publication?> GetAsync(PublicationId id, CancellationToken cancellationToken = default)
    {
        var key = RedisKeyHelper.Publication(id);
        var entries = await GetHashAsync(key);
        
        if (entries.Length == 0)
            return null;

        // Reconstitute, not Start: the target gateways were never stored, and Start
        // refuses an empty list — which it is right to do when creating one and wrong
        // to do when reading one. The caller caught that refusal in a bare catch, so
        // every publication read from storage vanished and the overview page failed
        // with "Publication must have at least one target gateway".
        //
        // The id and the started-at time come from storage too, because Start mints
        // a new id and stamps the current time: reading a record would rewrite its
        // identity and its position in the history.
        var publication = Publication.Reconstitute(
            PublicationId.From(Guid.Parse(GetEntry(entries, "Id"))),
            SnapshotVersion.From(int.Parse(GetEntry(entries, "SnapshotVersion"))),
            GetEntry(entries, "InitiatedBy"),
            DateTimeOffset.Parse(GetEntry(entries, "StartedAt"), CultureInfo.InvariantCulture)
        );

        var status = GetEntry(entries, "Status");
        if (status == PublicationStatus.Published.Value)
            publication.Complete();
        else if (status == PublicationStatus.Failed.Value)
        {
            // Not through RecordGatewayFailed: a failure is stored with no gateway
            // named, and that method needs one to blame — restoring a record that
            // way meant `Guid.Parse("")`, which threw and was swallowed, taking the
            // publication with it. The gateway states were never stored either, so
            // there is nothing to attribute the failure to. The reason is kept
            // verbatim rather than dressed up with a plausible gateway.
            publication.RecordFailureWithoutGateway(GetEntry(entries, "FailureReason"));
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

    /// <summary>
    /// Whether any publication has ever been addressed to this gateway.
    /// </summary>
    /// <remarks>
    /// Every publication is read, because "ever" is what matters: a gateway that
    /// was targeted once has history pointing at it even if a later publication
    /// left it out. A read that throws is treated as "no history" would be the
    /// dangerous direction, so a failure stops the answer rather than defaulting.
    /// </remarks>
    public async Task<bool> HasGatewayBeenPublishedToAsync(
        GatewayId gatewayId,
        CancellationToken cancellationToken = default)
    {
        var publications = await GetAllAsync(cancellationToken);
        return publications.Any(publication => publication.GatewayStates.ContainsKey(gatewayId));
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
            catch (Exception ex)
            {
                // A publication that cannot be restored must not disappear silently:
                // the list would report fewer than the index holds and nothing would
                // say why. Surfaced as a fault, so the read fails loudly.
                throw new InvalidOperationException(
                    $"Publication {id} could not be restored", ex);
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