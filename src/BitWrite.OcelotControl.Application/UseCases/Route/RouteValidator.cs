using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Domain.Services;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
// GlobalConfiguration is both a namespace and a type name, so it needs an alias.
using MinimalGlobalConfig = BitWrite.OcelotControl.Domain.Services.GlobalConfiguration;

namespace BitWrite.OcelotControl.Application.UseCases.Route;

/// <summary>
/// A single validation failure, tagged with the field that caused it.
/// </summary>
/// <param name="Field">
/// The request field at fault, matching the wizard's step names, or null when
/// the problem belongs to the route as a whole.
/// </param>
public sealed record RouteValidationError(string? Field, string Code, string Message);

public sealed record RouteValidationResult(
    bool IsValid,
    IReadOnlyList<RouteValidationError> Errors);

/// <summary>
/// Everything the checks below need, independent of whether the route already
/// exists. A draft fills this from the request body; a stored route fills it
/// from the aggregate.
/// </summary>
public sealed record RouteValidationInput(
    RouteConfiguration Configuration,
    /// The friendly key the operator typed, which is what conflicts are detected on.</summary>
    RouteKey Key,
    /// The id to exclude from conflict detection, or null for a draft.</summary>
    RouteId? ExistingRouteId,
    /// Capabilities in use, including ones the route DTO cannot carry yet.</summary>
    IReadOnlyList<string> Features);

/// <summary>
/// The one place route validation happens.
/// </summary>
/// <remarks>
/// Shared by the stored-route endpoint and the draft endpoint so the two can
/// never disagree about what makes a route valid. Every check here is
/// side-effect free: nothing is persisted and no events are raised, which is
/// what makes it safe to run against an unsaved draft.
/// </remarks>
public class RouteValidator
{
    // Capability name to the request field it comes from, so a failure points
    // at the step that can fix it.
    private static readonly Dictionary<string, string> FeatureFields = new()
    {
        ["authentication"] = "authenticationOptions.allowedScopes",
        ["authorization"] = "authorizationOptions",
        ["rate-limiting"] = "rateLimitOptions",
        ["qos"] = "qosOptions",
        ["caching"] = "cacheOptions",
        ["load-balancing"] = "loadBalancerOptions",
        // Each transformation has its own field, so a failure points at the
        // block that caused it rather than a generic "transformations".
        ["header-transformation"] = "headerTransformations",
        ["claim-transformation"] = "claimTransformations",
        ["query-string-transformation"] = "queryTransformations",
    };

    private readonly IRouteRepository _routeRepository;
    private readonly IServiceRepository _serviceRepository;
    private readonly IConfigurationBuilder _configurationBuilder;
    private readonly IRouteConflictDetector _routeConflictDetector;
    private readonly IConfigurationConsistencyValidator _consistencyValidator;
    private readonly IConfigurationCanonicalizer _canonicalizer;
    private readonly ISnapshotIntegrityVerifier _integrityVerifier;

    public RouteValidator(
        IRouteRepository routeRepository,
        IServiceRepository serviceRepository,
        IConfigurationBuilder configurationBuilder,
        IRouteConflictDetector routeConflictDetector,
        IConfigurationConsistencyValidator consistencyValidator,
        IConfigurationCanonicalizer canonicalizer,
        ISnapshotIntegrityVerifier integrityVerifier)
    {
        _routeRepository = routeRepository;
        _serviceRepository = serviceRepository;
        _configurationBuilder = configurationBuilder;
        _routeConflictDetector = routeConflictDetector;
        _consistencyValidator = consistencyValidator;
        _canonicalizer = canonicalizer;
        _integrityVerifier = integrityVerifier;
    }

    public async Task<RouteValidationResult> ValidateAsync(
        RouteValidationInput input,
        CancellationToken cancellationToken = default)
    {
        var configuration = input.Configuration;
        var errors = new List<RouteValidationError>();

        // 1. The service must exist, or the route points at nothing.
        var service = await _serviceRepository.GetAsync(configuration.ServiceId, cancellationToken);
        if (service == null)
        {
            errors.Add(new RouteValidationError(
                "serviceId",
                "SERVICE_NOT_FOUND",
                $"Service {configuration.ServiceId} not found"));
        }

        // 2. A draft is not stored yet, so nothing is excluded from conflicts.
        var existing = await _routeRepository.GetAllAsync(cancellationToken);
        var existingKeys = existing.Select(r => (r.Id, r.RouteKey)).ToList();
        var conflicts = _routeConflictDetector.DetectConflicts(
            input.Key,
            existingKeys,
            input.ExistingRouteId);
        errors.AddRange(conflicts.Select(conflict => new RouteValidationError(
            "key",
            "ROUTE_CONFLICT",
            $"Route conflict: {conflict.ToSignature()}")));

        // 3. Downstream targets.
        var targetErrors = _consistencyValidator.ValidateDownstreamTargets(
            configuration.DownstreamTargets);
        errors.AddRange(targetErrors.Select(error => new RouteValidationError(
            "downstreamTargets",
            error.Code,
            error.Message)));

        // 4. Build the Ocelot configuration, so the shape is validated too.
        //
        // A value the target version cannot express is reported against its own
        // field rather than failing the build, so the wizard can send the
        // operator to the step that can fix it. Publishing still fails, which is
        // the point: the rule is either expressible or it is refused.
        OcelotConfiguration ocelotConfig;
        try
        {
            ocelotConfig = _configurationBuilder.BuildConfiguration(
                new List<RouteConfiguration> { configuration },
                new MinimalGlobalConfig { BaseUrl = "", RequestIdKey = "" },
                OcelotVersion.V18_0);
        }
        catch (NotExpressibleException ex)
        {
            return new RouteValidationResult(
                false,
                new[] { new RouteValidationError(ex.Field, "NOT_EXPRESSIBLE", ex.Message) });
        }

        // 5. Every capability in use has to exist for the target Ocelot version.
        var capabilityErrors = _consistencyValidator.ValidateGlobalConfiguration(
            // The baseline the configuration was generated for.
            OcelotVersion.V18_0,
            input.Features);
        errors.AddRange(capabilityErrors.Select(error => new RouteValidationError(
            FieldForCode(error),
            error.Code,
            error.Message)));

        // 6. The built configuration must survive canonicalisation and hashing.
        try
        {
            var canonicalJson = _canonicalizer.CanonicalizeJson(ocelotConfig);
            // Hash the canonicalised string, which is what a snapshot stores.
            // Hashing the object would produce different text and the comparison
            // below could never succeed.
            var hash = _integrityVerifier.ComputeHash(canonicalJson);
            _integrityVerifier.ValidateIntegrity(canonicalJson, hash, SnapshotVersion.First());
        }
        catch (Exception ex)
        {
            errors.Add(new RouteValidationError(null, "CONFIGURATION_INVALID", ex.Message));
        }

        return new RouteValidationResult(errors.Count == 0, errors);
    }

    /// <summary>
    /// Points a capability failure at the field that enabled it, when it can be
    /// identified; otherwise the failure is about the route as a whole.
    /// </summary>
    private static string? FieldForCode(Domain.Services.ValidationError error)
    {
        foreach (var (feature, field) in FeatureFields)
        {
            if (error.Message.Contains(feature, StringComparison.OrdinalIgnoreCase) ||
                error.Code.Contains(feature, StringComparison.OrdinalIgnoreCase))
            {
                return field;
            }
        }

        return null;
    }
}
