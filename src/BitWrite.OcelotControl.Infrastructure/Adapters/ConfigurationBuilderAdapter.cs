using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Domain.Services;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using HttpMethod = BitWrite.OcelotControl.Domain.ValueObjects.Configuration.HttpMethod;
using GlobalConfig = BitWrite.OcelotControl.Domain.Services.GlobalConfiguration;

namespace BitWrite.OcelotControl.Infrastructure.Adapters;

/// <summary>
/// Adapter that implements IConfigurationBuilder using Domain.ConfigurationBuilder.
/// </summary>
public class ConfigurationBuilderAdapter : IConfigurationBuilder
{
    private readonly ConfigurationBuilder _domainBuilder;

    public ConfigurationBuilderAdapter(ConfigurationBuilder domainBuilder)
    {
        _domainBuilder = domainBuilder;
    }

    public OcelotConfiguration BuildConfiguration(
        IReadOnlyList<RouteConfiguration> routes,
        GlobalConfig globalConfig,
        OcelotVersion ocelotVersion)
    {
        return _domainBuilder.BuildConfiguration(routes, globalConfig, ocelotVersion);
    }

    public ConfigurationHash CalculateConfigurationHash(OcelotConfiguration configuration)
    {
        return _domainBuilder.CalculateConfigurationHash(configuration);
    }
}