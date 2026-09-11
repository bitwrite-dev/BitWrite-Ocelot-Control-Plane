using BitWrite.OcelotControl.Domain.Aggregates.Service;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;

namespace BitWrite.OcelotControl.Application.Interfaces;

public interface IServiceRepository
{
    Task<Service?> GetAsync(ServiceId id, CancellationToken cancellationToken = default);
    Task<List<Service>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Service service, CancellationToken cancellationToken = default);
    Task UpdateAsync(Service service, CancellationToken cancellationToken = default);
    Task DeleteAsync(ServiceId id, CancellationToken cancellationToken = default);
}