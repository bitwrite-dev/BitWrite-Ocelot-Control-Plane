using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Application.UseCases.Route;
using DomainRoute = BitWrite.OcelotControl.Domain.Aggregates.Route.Route;
using DomainGlobalConfig = BitWrite.OcelotControl.Domain.Services.GlobalConfiguration;
using BitWrite.OcelotControl.Domain.Events;
using BitWrite.OcelotControl.Domain.Services;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;

namespace BitWrite.OcelotControl.Application.UseCases.Route;

public class ValidateRouteCommandHandler
{
    private readonly IRouteRepository _routeRepository;
    private readonly IServiceRepository _serviceRepository;
    private readonly IConfigurationBuilder _configurationBuilder;
    private readonly IRouteConflictDetector _routeConflictDetector;
    private readonly IConfigurationConsistencyValidator _consistencyValidator;
    private readonly IOcelotCapabilityResolver _ocelotCapabilityResolver;
    private readonly IConfigurationCanonicalizer _canonicalizer;
    private readonly ISnapshotIntegrityVerifier _integrityVerifier;

    public ValidateRouteCommandHandler(
        IRouteRepository routeRepository,
        IServiceRepository serviceRepository,
        IConfigurationBuilder configurationBuilder,
        IRouteConflictDetector routeConflictDetector,
        IConfigurationConsistencyValidator consistencyValidator,
        IOcelotCapabilityResolver ocelotCapabilityResolver,
        IConfigurationCanonicalizer canonicalizer,
        ISnapshotIntegrityVerifier integrityVerifier)
    {
        _routeRepository = routeRepository;
        _serviceRepository = serviceRepository;
        _configurationBuilder = configurationBuilder;
        _routeConflictDetector = routeConflictDetector;
        _consistencyValidator = consistencyValidator;
        _ocelotCapabilityResolver = ocelotCapabilityResolver;
        _canonicalizer = canonicalizer;
        _integrityVerifier = integrityVerifier;
    }

    public async Task<ValidateRouteResponse> HandleAsync(ValidateRouteCommand command, CancellationToken cancellationToken = default)
    {
        var route = await _routeRepository.GetAsync(command.Id, cancellationToken);
        if (route == null)
        {
            return new ValidateRouteResponse(false, new List<string> { $"Route {command.Id} not found" });
        }

        var errors = new List<string>();

        // 1. Validate service reference
        var service = await _serviceRepository.GetAsync(route.ServiceId, cancellationToken);
        if (service == null)
        {
            errors.Add($"Service {route.ServiceId} not found");
        }

        // 2. Route conflict validation
        var allRoutes = await _routeRepository.GetAllAsync(cancellationToken);
        var routeKeys = allRoutes.Select(r => (r.Id, r.RouteKey)).ToList();
        var conflicts = _routeConflictDetector.DetectConflicts(route.RouteKey, routeKeys, route.Id);
        if (conflicts.Any())
        {
            errors.AddRange(conflicts.Select(c => $"Route conflict: {c.ToSignature()}"));
        }

        // 3. Downstream target validation
        var downstreamErrors = _consistencyValidator.ValidateDownstreamTargets(route.DownstreamTargets);
        errors.AddRange(downstreamErrors.Select(e => $"{e.Code}: {e.Message}"));

        // 4. Build Ocelot configuration for this route
        var routeConfig = MapToRouteConfiguration(route);
        var globalConfig = await _serviceRepository.GetAsync(route.ServiceId, cancellationToken);
        
        // For single route validation, we need a minimal global config
        var ocelotConfig = _configurationBuilder.BuildConfiguration(
            new List<RouteConfiguration> { routeConfig },
            new DomainGlobalConfig { BaseUrl = "", RequestIdKey = "" },
            OcelotVersion.V20_0);

        // 5. Ocelot capability validation
        var features = GetFeaturesFromRoute(route).Distinct().ToList();
        var capabilityErrors = _consistencyValidator.ValidateGlobalConfiguration(
            OcelotVersion.V20_0,
            features);
        errors.AddRange(capabilityErrors.Select(e => $"{e.Code}: {e.Message}"));

        // 6. JSON serialization validation
        try
        {
            var canonicalJson = _canonicalizer.CanonicalizeJson(ocelotConfig);
            var hash = _configurationBuilder.CalculateConfigurationHash(ocelotConfig);
            _integrityVerifier.ValidateIntegrity(canonicalJson, hash, SnapshotVersion.First());
        }
        catch (Exception ex)
        {
            errors.Add($"Configuration validation failed: {ex.Message}");
        }

        return new ValidateRouteResponse(errors.Count == 0, errors);
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

    private static IEnumerable<string> GetFeaturesFromRoute(DomainRoute route)
    {
        var features = new List<string>();

        if (route.AuthenticationOptions != null)
            features.Add("authentication");
        if (route.RateLimitOptions != null)
            features.Add("rate-limiting");
        if (route.QoSOptions != null)
            features.Add("qos");
        if (route.CacheOptions != null)
            features.Add("caching");
        if (route.LoadBalancerOptions != null)
            features.Add("load-balancing");
        if (route.HeaderOptions != null)
            features.Add("header-transformation");
        if (route.ClaimOptions != null)
            features.Add("claim-transformation");
        if (route.QueryOptions != null)
            features.Add("query-string-transformation");

        return features;
    }
}