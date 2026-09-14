using BitWrite.OcelotControl.Domain.Aggregates.RuntimeInstance;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;

namespace BitWrite.OcelotControl.Application.Interfaces;

public interface IRuntimeInstanceRepository
{
    Task<RuntimeInstance?> GetAsync(GatewayId id, CancellationToken cancellationToken = default);
    Task<List<RuntimeInstance>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(RuntimeInstance runtimeInstance, CancellationToken cancellationToken = default);
    Task UpdateAsync(RuntimeInstance runtimeInstance, CancellationToken cancellationToken = default);
    Task DeleteAsync(GatewayId id, CancellationToken cancellationToken = default);
}