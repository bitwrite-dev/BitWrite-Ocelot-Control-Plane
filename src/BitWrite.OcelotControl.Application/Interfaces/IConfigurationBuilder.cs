using BitWrite.OcelotControl.Domain.Aggregates.GlobalConfiguration;
using BitWrite.OcelotControl.Domain.Aggregates.Route;
using BitWrite.OcelotControl.Domain.Aggregates.Service;
using BitWrite.OcelotControl.Domain.Services;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using GlobalConfig = BitWrite.OcelotControl.Domain.Services.GlobalConfiguration;

namespace BitWrite.OcelotControl.Application.Interfaces;

public interface IConfigurationBuilder
{
    OcelotConfiguration BuildConfiguration(
        IReadOnlyList<RouteConfiguration> routes,
        GlobalConfig globalConfig,
        OcelotVersion ocelotVersion);

    ConfigurationHash CalculateConfigurationHash(OcelotConfiguration configuration);
}