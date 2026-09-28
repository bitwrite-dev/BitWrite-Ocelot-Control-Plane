using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Application.UseCases.Route;
using BitWrite.OcelotControl.Domain.Aggregates.GlobalConfiguration;
using DomainRoute = BitWrite.OcelotControl.Domain.Aggregates.Route.Route;
using BitWrite.OcelotControl.Domain.Aggregates.Service;
using DomainSnapshot = BitWrite.OcelotControl.Domain.Aggregates.Snapshot.Snapshot;
using BitWrite.OcelotControl.Domain.Events;
using BitWrite.OcelotControl.Domain.Services;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Domain.ValueObjects.Status;
using DomainGlobalConfig = BitWrite.OcelotControl.Domain.Services.GlobalConfiguration;
using DomainRouteConfig = BitWrite.OcelotControl.Domain.Services.RouteConfiguration;
using DomainDownstreamTarget = BitWrite.OcelotControl.Domain.ValueObjects.Configuration.DownstreamTarget;
using HttpMethod = BitWrite.OcelotControl.Domain.ValueObjects.Configuration.HttpMethod;

namespace BitWrite.OcelotControl.Application.UseCases.Snapshot;

public class CreateSnapshotCommandHandler
{
    private readonly IGlobalConfigurationRepository _globalConfigRepository;
    private readonly IRouteRepository _routeRepository;
    private readonly IServiceRepository _serviceRepository;
    private readonly ISnapshotRepository _snapshotRepository;
    private readonly IConfigurationBuilder _configurationBuilder;
    private readonly IRouteConflictDetector _routeConflictDetector;
    private readonly IConfigurationConsistencyValidator _consistencyValidator;
    private readonly IOcelotCapabilityResolver _ocelotCapabilityResolver;
    private readonly IConfigurationCanonicalizer _canonicalizer;
    private readonly ISystemSettingsRepository _systemSettings;
    private readonly ISnapshotIntegrityVerifier _integrityVerifier;
    private readonly ISnapshotVersionAllocator _versionAllocator;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public CreateSnapshotCommandHandler(
        IGlobalConfigurationRepository globalConfigRepository,
        IRouteRepository routeRepository,
        IServiceRepository serviceRepository,
        ISnapshotRepository snapshotRepository,
        IConfigurationBuilder configurationBuilder,
        IRouteConflictDetector routeConflictDetector,
        IConfigurationConsistencyValidator consistencyValidator,
        IOcelotCapabilityResolver ocelotCapabilityResolver,
        IConfigurationCanonicalizer canonicalizer,
        ISystemSettingsRepository systemSettings,
        ISnapshotIntegrityVerifier integrityVerifier,
        ISnapshotVersionAllocator versionAllocator,
        IDomainEventDispatcher eventDispatcher)
    {
        _globalConfigRepository = globalConfigRepository;
        _routeRepository = routeRepository;
        _serviceRepository = serviceRepository;
        _snapshotRepository = snapshotRepository;
        _configurationBuilder = configurationBuilder;
        _routeConflictDetector = routeConflictDetector;
        _consistencyValidator = consistencyValidator;
        _ocelotCapabilityResolver = ocelotCapabilityResolver;
        _canonicalizer = canonicalizer;
        _systemSettings = systemSettings;
        _integrityVerifier = integrityVerifier;
        _versionAllocator = versionAllocator;
        _eventDispatcher = eventDispatcher;
    }

    public async Task<SnapshotVersion> HandleAsync(CreateSnapshotCommand command, CancellationToken cancellationToken = default)
    {
        // 1. Load all management state
        var globalConfig = await _globalConfigRepository.GetAsync(cancellationToken);
        var routes = await _routeRepository.GetAllAsync(cancellationToken);
        var services = await _serviceRepository.GetAllAsync(cancellationToken);

        // Convert to RouteConfiguration for ConfigurationBuilder
        var routeConfigs = routes.Select(RouteConfigurationMapper.Map).ToList();

        // 2. Build Ocelot configuration using Domain Service
        var ocelotConfig = _configurationBuilder.BuildConfiguration(
            routeConfigs,
            MapToGlobalConfiguration(globalConfig),
            (await _systemSettings.GetAsync(cancellationToken)).OcelotVersion);

        // 3. 5 Validation Layers
        // 3.1 Domain Validation - each aggregate validates itself
        // (Already validated during aggregate operations)

        // 3.2 Route Conflict Validation
        var routeKeys = routes.Select(r => (r.Id, r.RouteKey)).ToList();
        var allConflicts = new List<RouteKey>();
        
        foreach (var (id, key) in routeKeys)
        {
            var conflicts = _routeConflictDetector.DetectConflicts(key, routeKeys, id);
            allConflicts.AddRange(conflicts);
        }

        if (allConflicts.Any())
        {
            var failedEvent = new SnapshotValidationFailed(
                SnapshotVersion.First(),
                $"Route conflicts detected: {string.Join(", ", allConflicts.Select(c => c.ToSignature()))}"
            );
            await _eventDispatcher.DispatchAsync(failedEvent, cancellationToken);
            throw new InvalidOperationException($"Route conflicts detected: {string.Join(", ", allConflicts.Select(c => c.ToSignature()))}");
        }

        // 3.3 Reference Validation
        var routeServiceIds = routes.Select(r => r.ServiceId);
        var existingServiceIds = services.Select(s => s.Id);
        var serviceRefErrors = _consistencyValidator.ValidateServiceReferences(routeServiceIds, existingServiceIds);
        
        var downstreamTargets = routes.SelectMany(r => r.DownstreamTargets);
        var downstreamErrors = _consistencyValidator.ValidateDownstreamTargets(downstreamTargets);
        
        var allErrors = serviceRefErrors.Concat(downstreamErrors).ToList();
        
        if (allErrors.Any())
        {
            var failedEvent = new SnapshotValidationFailed(
                SnapshotVersion.First(),
                $"Reference validation failed: {string.Join(", ", allErrors.Select(e => $"{e.Code}: {e.Message}"))}"
            );
            await _eventDispatcher.DispatchAsync(failedEvent, cancellationToken);
            throw new InvalidOperationException($"Reference validation failed: {string.Join(", ", allErrors.Select(e => $"{e.Code}: {e.Message}"))}");
        }

        // 3.4 Ocelot Configuration Validation - Check capabilities
        var features = routes.SelectMany(r => GetFeaturesFromRoute(r)).Distinct().ToList();
        var capabilityErrors = _consistencyValidator.ValidateGlobalConfiguration(
            OcelotVersion.V20_0,
            features);
        
        if (capabilityErrors.Any())
        {
            var failedEvent = new SnapshotValidationFailed(
                SnapshotVersion.First(),
                $"Ocelot capability validation failed: {string.Join(", ", capabilityErrors.Select(e => $"{e.Code}: {e.Message}"))}"
            );
            await _eventDispatcher.DispatchAsync(failedEvent, cancellationToken);
            throw new InvalidOperationException($"Ocelot capability validation failed: {string.Join(", ", capabilityErrors.Select(e => $"{e.Code}: {e.Message}"))}");
        }

        // 4. Canonicalize once, then hash exactly the string that is stored.
        //
        // The stored hash must cover Snapshot.Content, because that is what
        // VerifyIntegrity hashes when it re-reads a snapshot. Hashing the
        // configuration object instead — CalculateConfigurationHash canonicalises
        // it into a different, human-readable text format — guaranteed a
        // mismatch, so every snapshot failed its own integrity check.
        var canonicalJson = _canonicalizer.CanonicalizeJson(ocelotConfig);
        var hash = _integrityVerifier.ComputeHash(canonicalJson);

        // 5. Version Allocation
        var version = _versionAllocator.AllocateNext();

// 6. Create Snapshot aggregate
        var snapshot = DomainSnapshot.Create(
            canonicalJson,
            hash,
            version,
            command.InitiatedBy
        );

        // 7. Persist Snapshot
        await _snapshotRepository.AddAsync(snapshot, cancellationToken);

        // 8. Raise domain event
        var createdEvent = new SnapshotCreated(version, hash, command.InitiatedBy);
        await _eventDispatcher.DispatchAsync(createdEvent, cancellationToken);

        // 9. Raise audit event
        var auditEvent = new AuditRecorded(
            command.InitiatedBy,
            "CreateSnapshot",
            "Snapshot",
            version.Value.ToString(),
            "Success"
        );
        await _eventDispatcher.DispatchAsync(auditEvent, cancellationToken);

        return version;
    }

    private DomainGlobalConfig MapToGlobalConfiguration(BitWrite.OcelotControl.Domain.Aggregates.GlobalConfiguration.GlobalConfiguration globalConfig)
    {
        return new DomainGlobalConfig
        {
            BaseUrl = globalConfig.BaseUrl,
            RequestIdKey = globalConfig.RequestIdKey
        };
    }

    private IEnumerable<string> GetFeaturesFromRoute(DomainRoute route)
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