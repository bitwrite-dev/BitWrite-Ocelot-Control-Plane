using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Application.UseCases.Route;
using DomainRoute = BitWrite.OcelotControl.Domain.Aggregates.Route.Route;
using DomainGlobalConfig = BitWrite.OcelotControl.Domain.Services.GlobalConfiguration;
using BitWrite.OcelotControl.Domain.Services;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;

namespace BitWrite.OcelotControl.Application.UseCases.Route;

public class PreviewRouteQueryHandler
{
    private readonly IRouteRepository _routeRepository;
    private readonly IServiceRepository _serviceRepository;
    private readonly IConfigurationBuilder _configurationBuilder;
    private readonly IConfigurationCanonicalizer _canonicalizer;

    public PreviewRouteQueryHandler(
        IRouteRepository routeRepository,
        IServiceRepository serviceRepository,
        IConfigurationBuilder configurationBuilder,
        IConfigurationCanonicalizer canonicalizer)
    {
        _routeRepository = routeRepository;
        _serviceRepository = serviceRepository;
        _configurationBuilder = configurationBuilder;
        _canonicalizer = canonicalizer;
    }

    public async Task<PreviewRouteResponse?> HandleAsync(PreviewRouteQuery query, CancellationToken cancellationToken = default)
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

        // Build Ocelot configuration for this single route
        var routeConfig = MapToRouteConfiguration(route);
        var ocelotConfig = _configurationBuilder.BuildConfiguration(
            new List<RouteConfiguration> { routeConfig },
            new DomainGlobalConfig { BaseUrl = "", RequestIdKey = "" },
            OcelotVersion.V20_0);

        // Canonicalize and return JSON
        var canonicalJson = _canonicalizer.CanonicalizeJson(ocelotConfig);

        return new PreviewRouteResponse(canonicalJson);
    }

    private static RouteConfiguration MapToRouteConfiguration(DomainRoute route)
    {
        return new RouteConfiguration
        {
            Id = route.Id,
            Host = route.Host,
            Method = route.Method,
            UpstreamPath = route.UpstreamPath,
            ServiceId = route.ServiceId,
            DownstreamTargets = route.DownstreamTargets.Select(t => DownstreamTarget.Create(t.Scheme, t.Host, t.Port, t.Path)).ToList(),
            AuthenticationOptions = route.AuthenticationOptions,
            RateLimitOptions = route.RateLimitOptions,
            QoSOptions = route.QoSOptions,
            CacheOptions = route.CacheOptions,
            LoadBalancerOptions = route.LoadBalancerOptions
        };
    }
}