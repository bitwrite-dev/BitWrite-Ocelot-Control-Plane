using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Application.UseCases.Route;
using BitWrite.OcelotControl.Domain.Aggregates.GlobalConfiguration;
using BitWrite.OcelotControl.Domain.Aggregates.Service;
using BitWrite.OcelotControl.Domain.Events;
using BitWrite.OcelotControl.Domain.Services;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Domain.ValueObjects.Status;
using DomainDownstreamTarget = BitWrite.OcelotControl.Domain.ValueObjects.Configuration.DownstreamTarget;
using DomainGlobalConfig = BitWrite.OcelotControl.Domain.Services.GlobalConfiguration;
using DomainRoute = BitWrite.OcelotControl.Domain.Aggregates.Route.Route;
using DomainRouteConfig = BitWrite.OcelotControl.Domain.Services.RouteConfiguration;
using DomainSnapshot = BitWrite.OcelotControl.Domain.Aggregates.Snapshot.Snapshot;
using DomainValidationResult = BitWrite.OcelotControl.Domain.Aggregates.Snapshot.ValidationResult;
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

        // 3.2–3.4 Run every rule, then decide.
        //
        // These used to throw at the first failure, which meant an operator fixing
        // one problem was told about the next one only after fixing the first —
        // and the results were computed and then thrown away, so a snapshot that
        // failed left no record of why. Collecting first costs nothing and answers
        // everything at once.
        var results = new List<DomainValidationResult>();

        // 3.2 Route conflicts.
        var routeKeys = routes.Select(r => (r.Id, r.RouteKey)).ToList();
        var allConflicts = new List<RouteKey>();

        foreach (var (id, key) in routeKeys)
        {
            allConflicts.AddRange(_routeConflictDetector.DetectConflicts(key, routeKeys, id));
        }

        results.Add(new DomainValidationResult
        {
            Rule = "RouteConflicts",
            IsValid = allConflicts.Count == 0,
            Message = allConflicts.Count == 0
                ? null
                : $"Overlapping routes: {string.Join(", ", allConflicts.Select(c => c.ToSignature()))}",
        });

        // 3.3 References.
        var referenceErrors = _consistencyValidator
            .ValidateServiceReferences(routes.Select(r => r.ServiceId), services.Select(s => s.Id))
            .Concat(_consistencyValidator.ValidateDownstreamTargets(
                routes.SelectMany(r => r.DownstreamTargets)))
            .ToList();

        results.Add(new DomainValidationResult
        {
            Rule = "References",
            IsValid = referenceErrors.Count == 0,
            Message = referenceErrors.Count == 0
                ? null
                : string.Join("; ", referenceErrors.Select(e => $"{e.Code}: {e.Message}")),
        });

        // 3.4 Capabilities, checked against the configured version rather than a
        // hard-coded one. It used V20_0 whatever the installation had chosen, so a
        // deployment targeting 18 was validated against 20's capabilities.
        var capabilityErrors = _consistencyValidator.ValidateGlobalConfiguration(
            (await _systemSettings.GetAsync(cancellationToken)).OcelotVersion ?? OcelotVersion.V18_0,
            routes.SelectMany(GetFeaturesFromRoute).Distinct().ToList());

        results.Add(new DomainValidationResult
        {
            Rule = "OcelotCapabilities",
            IsValid = capabilityErrors.Count == 0,
            Message = capabilityErrors.Count == 0
                ? null
                : string.Join("; ", capabilityErrors.Select(e => $"{e.Code}: {e.Message}")),
        });

        // A warning rather than a failure: sealing an artifact with no routes is
        // occasionally deliberate — clearing a gateway — so the operator is told and
        // decides. A rule that blocked here would invent a restriction the domain
        // does not have.
        results.Add(new DomainValidationResult
        {
            Rule = "HasContent",
            IsValid = routes.Count > 0,
            Message = routes.Count == 0
                ? "Warning: no routes are configured. This snapshot would publish an empty configuration to every gateway."
                : null,
        });

        // `HasContent` is a warning, not a failure: it says the snapshot would
        // publish an empty configuration, but sealing one is occasionally how a
        // gateway gets cleared, so the operator is told and decides. Treating it as
        // a failure here would make that impossible — and would have quietly
        // contradicted the create page, which shows the same rule as a warning and
        // lets the step through.
        var failures = results
            .Where(r => !r.IsValid && !r.Message.StartsWith("Warning", StringComparison.Ordinal))
            .ToList();

        if (failures.Any())
        {
            var summary = string.Join("; ", failures.Select(f => $"{f.Rule}: {f.Message}"));
            await _eventDispatcher.DispatchAsync(
                new SnapshotValidationFailed(SnapshotVersion.First(), summary),
                cancellationToken);
            throw new InvalidOperationException($"Snapshot validation failed: {summary}");
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
            command.InitiatedBy,
            validationResults: results);

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
