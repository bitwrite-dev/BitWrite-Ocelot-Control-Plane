using BitWrite.OcelotControl.Domain.Aggregates.GlobalConfiguration;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;

namespace BitWrite.OcelotControl.Application.Interfaces;

public interface IGlobalConfigurationRepository
{
    Task<GlobalConfiguration> GetAsync(CancellationToken cancellationToken = default);
    Task AddAsync(GlobalConfiguration globalConfiguration, CancellationToken cancellationToken = default);
    Task UpdateAsync(GlobalConfiguration globalConfiguration, CancellationToken cancellationToken = default);
}