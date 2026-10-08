using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Application.UseCases.Route;
using BitWrite.OcelotControl.Domain.Aggregates.GlobalConfiguration;
using BitWrite.OcelotControl.Domain.Services;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using DomainGlobalConfig = BitWrite.OcelotControl.Domain.Services.GlobalConfiguration;
using DomainRoute = BitWrite.OcelotControl.Domain.Aggregates.Route.Route;
using DomainService = BitWrite.OcelotControl.Domain.Aggregates.Service.Service;
using HttpMethod = BitWrite.OcelotControl.Domain.ValueObjects.Configuration.HttpMethod;

namespace BitWrite.OcelotControl.Application.UseCases.Snapshot;

/// <summary>
/// Builds and validates the artifact a snapshot would contain, without storing it.
/// </summary>
/// <remarks>
/// Creating a snapshot used to be one call that built, validated, hashed, versioned
/// and stored in a single step, which left the operator no point at which to look
/// at what was about to be sealed. This handler does everything except the last
/// two of those, so the management state can be reviewed before it becomes
/// immutable.
/// <para>
/// It deliberately does not allocate a version. Allocating here would burn a
/// number on a preview that may be abandoned, and the numbers are what a rollback
/// names.
/// </para>
/// <para>
/// The result is a preview of what the state produces right now. It is not a
/// reservation: the management state can change between this call and the create
/// that follows, so the create validates again rather than trusting a preview it
/// may never have received.
/// </para>
/// </remarks>
public class PreviewSnapshotCommandHandler
{
    private readonly IGlobalConfigurationRepository _globalConfigRepository;
    private readonly IRouteRepository _routeRepository;
    private readonly IServiceRepository _serviceRepository;
    private readonly IConfigurationBuilder _configurationBuilder;
    private readonly IRouteConflictDetector _routeConflictDetector;
    private readonly IConfigurationConsistencyValidator _consistencyValidator;
    private readonly IConfigurationCanonicalizer _canonicalizer;
    private readonly ISnapshotIntegrityVerifier _integrityVerifier;
    private readonly ISystemSettingsRepository _systemSettings;
    private readonly ISnapshotVersionAllocator _versionAllocator;

    public PreviewSnapshotCommandHandler(
        IGlobalConfigurationRepository globalConfigRepository,
        IRouteRepository routeRepository,
        IServiceRepository serviceRepository,
        IConfigurationBuilder configurationBuilder,
        IRouteConflictDetector routeConflictDetector,
        IConfigurationConsistencyValidator consistencyValidator,
        IConfigurationCanonicalizer canonicalizer,
        ISnapshotIntegrityVerifier integrityVerifier,
        ISystemSettingsRepository systemSettings,
        ISnapshotVersionAllocator versionAllocator)
    {
        _globalConfigRepository = globalConfigRepository;
        _routeRepository = routeRepository;
        _serviceRepository = serviceRepository;
        _configurationBuilder = configurationBuilder;
        _routeConflictDetector = routeConflictDetector;
        _consistencyValidator = consistencyValidator;
        _canonicalizer = canonicalizer;
        _integrityVerifier = integrityVerifier;
        _systemSettings = systemSettings;
        _versionAllocator = versionAllocator;
    }

    public async Task<PreviewSnapshotResponse> HandleAsync(
        CancellationToken cancellationToken = default)
    {
        var globalConfig = await _globalConfigRepository.GetAsync(cancellationToken);
        var routes = await _routeRepository.GetAllAsync(cancellationToken);
        var services = await _serviceRepository.GetAllAsync(cancellationToken);
        var ocelotVersion = (await _systemSettings.GetAsync(cancellationToken)).OcelotVersion;

        var routeConfigs = routes.Select(RouteConfigurationMapper.Map).ToList();

        // An unbuildable configuration cannot be validated, so this is reported
        // as a failed rule rather than as an exception: the page is supposed to
        // show why it cannot go on, not to fall over.
        var results = new List<ValidationResult>();
        OcelotConfiguration? ocelotConfig = null;
        string? content = null;
        string? hash = null;

        try
        {
            // Until first-run has chosen a version there is no shape to emit, and
            // `BuildConfiguration` takes a non-nullable one. Reporting the gap
            // keeps the reason on the page; passing null through would be a null
            // reference with nothing to say about it.
            var version = ocelotVersion
                ?? throw new InvalidOperationException(
                    "No Ocelot version has been chosen yet. Complete first-run setup before " +
                    "creating a snapshot.");

            ocelotConfig = _configurationBuilder.BuildConfiguration(
                routeConfigs,
                MapToGlobalConfiguration(globalConfig),
                version);

            // Canonicalise once and hash the exact string that would be stored, so
            // the hash shown here is the hash the snapshot would carry. Hashing the
            // object instead would produce a different text and never match.
            content = _canonicalizer.CanonicalizeJson(ocelotConfig);
            hash = _integrityVerifier.ComputeHash(content);
        }
        catch (Exception ex)
        {
            results.Add(new ValidationResult(
                "ConfigurationBuild",
                false,
                $"The management state could not be turned into a configuration: {ex.Message}"));
        }

        if (content is not null)
        {
            results.AddRange(ValidateRouteConflicts(routes));
            results.AddRange(ValidateReferences(routes, services));
            results.AddRange(ValidateCapabilities(routes, ocelotVersion));
        }

        results.Add(ValidateNotEmpty(routes));

        // The composition is read out of the content that was actually built,
        // rather than counted from the aggregates, so the numbers on the preview
        // are the numbers the artifact carries.
        var composition = content is null
            ? new SnapshotCompositionSummary(0, 0, Array.Empty<string>())
            : new SnapshotCompositionSummary(
                SnapshotComposition.RouteCount(content) ?? 0,
                SnapshotComposition.ServiceCount(content) ?? 0,
                SnapshotComposition.PluginVersions(content));

        // Not consumed: a preview does not allocate. The next number is reported
        // as what the snapshot would take, which is the number a rollback names,
        // so it must not be spent on a preview that gets abandoned.
        var nextVersion = _versionAllocator.CurrentVersion.Value + 1;

        // Only the rules that block. The empty-state warning is shown on the page
        // without gating the step, because clearing a gateway is a legitimate
        // reason to seal an artifact with no routes.
        var blocking = results.Where(result => result.Rule != "HasContent").ToList();

        return new PreviewSnapshotResponse(
            content,
            hash,
            composition,
            results,
            blocking.All(result => result.IsValid),
            // Null until first-run. Calling ToString on it was the null reference
            // the create page surfaced, and it happened after every rule had run —
            // so a preview that reported everything else could not be shown at all.
            ocelotVersion?.ToString() ?? "not chosen",
            nextVersion);
    }

    private IEnumerable<ValidationResult> ValidateRouteConflicts(IReadOnlyList<DomainRoute> routes)
    {
        var routeKeys = routes.Select(route => (route.Id, route.RouteKey)).ToList();
        var conflicts = new List<RouteKey>();

        foreach (var (id, key) in routeKeys)
        {
            conflicts.AddRange(_routeConflictDetector.DetectConflicts(key, routeKeys, id));
        }

        // Reported as a rule that failed rather than an exception. Throwing here
        // would make "your routes conflict" indistinguishable from "the server is
        // broken", and only one of those is the operator's to fix.
        return
        [
            new ValidationResult(
                "RouteConflicts",
                conflicts.Count == 0,
                conflicts.Count == 0
                    ? null
                    : $"Overlapping routes: {string.Join(", ", conflicts.Select(conflict => conflict.ToSignature()))}")
        ];
    }

    private IEnumerable<ValidationResult> ValidateReferences(
        IReadOnlyList<DomainRoute> routes,
        IReadOnlyList<DomainService> services)
    {
        var serviceErrors = _consistencyValidator.ValidateServiceReferences(
            routes.Select(route => route.ServiceId),
            services.Select(service => service.Id));

        var targetErrors = _consistencyValidator.ValidateDownstreamTargets(
            routes.SelectMany(route => route.DownstreamTargets));

        var errors = serviceErrors.Concat(targetErrors).ToList();

        return
        [
            new ValidationResult(
                "References",
                errors.Count == 0,
                errors.Count == 0
                    ? null
                    : string.Join("; ", errors.Select(error => $"{error.Code}: {error.Message}")))
        ];
    }

    private IEnumerable<ValidationResult> ValidateCapabilities(
        IReadOnlyList<DomainRoute> routes,
        OcelotVersion ocelotVersion)
    {
        // Only reached once something was built, which cannot happen without a
        // version: the builder refuses first. So this is never null here, and a
        // guard here would be a branch nothing can reach.
        //
        // Checked against the configured version rather than a hard-coded one, so
        // a feature that the chosen version cannot express is caught here instead
        // of by a gateway refusing to start.
        var errors = _consistencyValidator.ValidateGlobalConfiguration(
            ocelotVersion,
            routes.SelectMany(GetFeaturesFromRoute).Distinct().ToList());

        return
        [
            new ValidationResult(
                "OcelotCapabilities",
                errors.Count == 0,
                errors.Count == 0
                    ? null
                    : string.Join("; ", errors.Select(error => $"{error.Code}: {error.Message}")))
        ];
    }

    /// <summary>
    /// A snapshot with no routes in it is almost never intended.
    /// </summary>
    /// <remarks>
    /// Reported as a warning, and deliberately excluded from the validity flag:
    /// sealing an empty artifact is occasionally the point — clearing a gateway —
    /// so the operator is told and decides. A rule that blocked the step would
    /// make that impossible and would be inventing a restriction the domain does
    /// not have.
    /// </remarks>
    private static ValidationResult ValidateNotEmpty(IReadOnlyList<DomainRoute> routes)
    {
        return new ValidationResult(
            "HasContent",
            routes.Count > 0,
            routes.Count == 0
                ? "Warning: no routes are configured. This snapshot would publish an empty configuration to every gateway."
                : null);
    }

    private static DomainGlobalConfig MapToGlobalConfiguration(
        BitWrite.OcelotControl.Domain.Aggregates.GlobalConfiguration.GlobalConfiguration globalConfig) =>
        new()
        {
            BaseUrl = globalConfig.BaseUrl,
            RequestIdKey = globalConfig.RequestIdKey
        };

    private static IEnumerable<string> GetFeaturesFromRoute(DomainRoute route)
    {
        if (route.AuthenticationOptions != null) yield return "authentication";
        if (route.RateLimitOptions != null) yield return "rate-limiting";
        if (route.QoSOptions != null) yield return "qos";
        if (route.CacheOptions != null) yield return "caching";
        if (route.LoadBalancerOptions != null) yield return "load-balancing";
        if (route.HeaderOptions != null) yield return "header-transformation";
        if (route.ClaimOptions != null) yield return "claim-transformation";
        if (route.QueryOptions != null) yield return "query-string-transformation";
    }
}
