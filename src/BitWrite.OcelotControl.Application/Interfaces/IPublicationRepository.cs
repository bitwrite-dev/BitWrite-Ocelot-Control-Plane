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
}