using AppIRuntimeInstanceRepository = BitWrite.OcelotControl.Application.Interfaces.IRuntimeInstanceRepository;
using AppRuntimeInstance = BitWrite.OcelotControl.Domain.Aggregates.RuntimeInstance.RuntimeInstance;
using AppGatewayId = BitWrite.OcelotControl.Domain.ValueObjects.Identity.GatewayId;
using InfraIRuntimeInstanceRepository = BitWrite.OcelotControl.Infrastructure.Repositories.IRuntimeInstanceRepository;

namespace BitWrite.OcelotControl.Infrastructure.Adapters;

/// <summary>
/// Adapter that implements Application.Interfaces.IRuntimeInstanceRepository using Infrastructure.Repositories.IRuntimeInstanceRepository.
/// </summary>
public class RuntimeInstanceRepositoryAdapter : AppIRuntimeInstanceRepository
{
    private readonly InfraIRuntimeInstanceRepository _infraRepository;

    public RuntimeInstanceRepositoryAdapter(InfraIRuntimeInstanceRepository infraRepository)
    {
        _infraRepository = infraRepository;
    }

    public Task<AppRuntimeInstance?> GetAsync(AppGatewayId id, CancellationToken cancellationToken = default)
    {
        return _infraRepository.GetAsync(id, cancellationToken);
    }

    public Task<List<AppRuntimeInstance>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return _infraRepository.GetAllAsync(cancellationToken);
    }

    public Task AddAsync(AppRuntimeInstance runtimeInstance, CancellationToken cancellationToken = default)
    {
        return _infraRepository.AddAsync(runtimeInstance, cancellationToken);
    }

    public Task UpdateAsync(AppRuntimeInstance runtimeInstance, CancellationToken cancellationToken = default)
    {
        return _infraRepository.UpdateAsync(runtimeInstance, cancellationToken);
    }

    public Task DeleteAsync(AppGatewayId id, CancellationToken cancellationToken = default)
    {
        return _infraRepository.DeleteAsync(id, cancellationToken);
    }
}