using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Application.UseCases.GlobalConfiguration;
using DomainGlobalConfig = BitWrite.OcelotControl.Domain.Aggregates.GlobalConfiguration.GlobalConfiguration;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.FeatureConfig;

namespace BitWrite.OcelotControl.Application.UseCases.GlobalConfiguration;

public class GetGlobalConfigurationQueryHandler
{
    private readonly IGlobalConfigurationRepository _globalConfigRepository;

    public GetGlobalConfigurationQueryHandler(IGlobalConfigurationRepository globalConfigRepository)
    {
        _globalConfigRepository = globalConfigRepository;
    }

    public async Task<GlobalConfigurationResponse> HandleAsync(GetGlobalConfigurationQuery query, CancellationToken cancellationToken = default)
    {
        var config = await _globalConfigRepository.GetAsync(cancellationToken);
        return MapToResponse(config);
    }

    private static GlobalConfigurationResponse MapToResponse(DomainGlobalConfig config)
    {
        return new GlobalConfigurationResponse(
            config.Id.ToString(),
            config.BaseUrl,
            config.RequestIdKey,
            config.DownstreamScheme,
            config.Timeout,
            config.RateLimit,
            config.QoS,
            config.HttpHandler,
            config.ServiceDiscovery,
            config.UpdatedAt
        );
    }
}