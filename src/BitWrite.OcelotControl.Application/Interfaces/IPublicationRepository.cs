using BitWrite.OcelotControl.Domain.Aggregates.Publication;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;

namespace BitWrite.OcelotControl.Application.Interfaces;

public interface IPublicationRepository
{
    Task<Publication?> GetAsync(PublicationId id, CancellationToken cancellationToken = default);
    Task<Publication?> GetLatestAsync(CancellationToken cancellationToken = default);
    Task<List<Publication>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Publication publication, CancellationToken cancellationToken = default);
    Task UpdateAsync(Publication publication, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether any publication has been addressed to this gateway.
    /// </summary>
    /// <remarks>
    /// Gateway deletion depends on this, and the answer spans every publication
    /// ever written, so it cannot come from an aggregate.
    /// </remarks>
    Task<bool> HasGatewayBeenPublishedToAsync(
        GatewayId gatewayId,
        CancellationToken cancellationToken = default);
}