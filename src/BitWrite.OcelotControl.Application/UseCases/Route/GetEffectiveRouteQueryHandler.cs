using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Application.UseCases.Route;
using BitWrite.OcelotControl.Domain.Services;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using DomainGlobalConfig = BitWrite.OcelotControl.Domain.Services.GlobalConfiguration;
using DomainRoute = BitWrite.OcelotControl.Domain.Aggregates.Route.Route;

namespace BitWrite.OcelotControl.Application.UseCases.Route;

public class GetEffectiveRouteQueryHandler
{
    private readonly IRouteRepository _routeRepository;
    private readonly IServiceRepository _serviceRepository;
    private readonly IConfigurationBuilder _configurationBuilder;
    private readonly IConfigurationCanonicalizer _canonicalizer;
    private readonly ISystemSettingsRepository _systemSettings;

    public GetEffectiveRouteQueryHandler(
        IRouteRepository routeRepository,
        IServiceRepository serviceRepository,
        IConfigurationBuilder configurationBuilder,
        IConfigurationCanonicalizer canonicalizer,
        ISystemSettingsRepository systemSettings)
    {
        _routeRepository = routeRepository;
        _serviceRepository = serviceRepository;
        _configurationBuilder = configurationBuilder;
        _canonicalizer = canonicalizer;
        _systemSettings = systemSettings;
    }

    public async Task<GetEffectiveRouteResponse?> HandleAsync(GetEffectiveRouteQuery query, CancellationToken cancellationToken = default)
    {
        var route = await _routeRepository.GetAsync(query.Id, cancellationToken);
        if (route == null)
            return null;

        // Validate service exists
        var service = await _serviceRepository.GetAsync(route.ServiceId, cancellationToken);
        if (service == null)
        {
            throw new InvalidOperationException($"Service {route.ServiceId} not found");
        }

        // Build Ocelot configuration for this single route (effective config includes all validation)
        var routeConfig = RouteConfigurationMapper.Map(route);
        var ocelotConfig = _configurationBuilder.BuildConfiguration(
            new List<RouteConfiguration> { routeConfig },
            new DomainGlobalConfig { BaseUrl = "", RequestIdKey = "" },
            (await _systemSettings.GetAsync(cancellationToken)).OcelotVersion);

        // Canonicalize and return JSON
        var canonicalJson = _canonicalizer.CanonicalizeJson(ocelotConfig);

        return new GetEffectiveRouteResponse(canonicalJson);
    }

}
