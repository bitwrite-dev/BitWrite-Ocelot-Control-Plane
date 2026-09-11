using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Domain.Exceptions;

namespace BitWrite.OcelotControl.Domain.Services;

/// <summary>
/// Validates consistency between aggregates (§12.4 Reference Validation).
/// Ensures all referenced services and configuration objects exist and are valid.
/// </summary>
public class ConfigurationConsistencyValidator
{
    /// <summary>
    /// Validates that all service references in routes are valid.
    /// </summary>
    /// <param name="routeServiceIds">Service IDs referenced by routes.</param>
    /// <param name="existingServiceIds">All existing service IDs in the system.</param>
    /// <returns>A list of validation errors.</returns>
    public IReadOnlyList<ValidationError> ValidateServiceReferences(
        IEnumerable<ServiceId> routeServiceIds,
        IEnumerable<ServiceId> existingServiceIds)
    {
        var errors = new List<ValidationError>();
        var existingSet = existingServiceIds.ToHashSet();

        foreach (var serviceId in routeServiceIds.Distinct())
        {
            if (!existingSet.Contains(serviceId))
            {
                errors.Add(new ValidationError(
                    "SERVICE_NOT_FOUND",
                    $"Service {serviceId} referenced by route does not exist"));
            }
        }

        return errors.AsReadOnly();
    }

    /// <summary>
    /// Validates that all downstream targets are valid.
    /// </summary>
    /// <param name="targets">Downstream targets to validate.</param>
    /// <returns>A list of validation errors.</returns>
    public IReadOnlyList<ValidationError> ValidateDownstreamTargets(
        IEnumerable<DownstreamTarget> targets)
    {
        var errors = new List<ValidationError>();

        foreach (var target in targets)
        {
            if (string.IsNullOrWhiteSpace(target.Host))
            {
                errors.Add(new ValidationError(
                    "INVALID_TARGET_HOST",
                    $"Downstream target has empty host: {target}"));
            }

            if (target.Port <= 0 || target.Port > 65535)
            {
                errors.Add(new ValidationError(
                    "INVALID_TARGET_PORT",
                    $"Downstream target has invalid port {target.Port}: {target}"));
            }
        }

        return errors.AsReadOnly();
    }

    /// <summary>
    /// Validates route configuration consistency.
    /// </summary>
    /// <param name="routeId">The route being validated.</param>
    /// <param name="serviceId">The service reference.</param>
    /// <param name="upstreamPath">The upstream path.</param>
    /// <param name="downstreamTargets">The downstream targets.</param>
    /// <returns>A list of validation errors.</returns>
    public IReadOnlyList<ValidationError> ValidateRouteConfiguration(
        RouteId routeId,
        ServiceId serviceId,
        UpstreamPath upstreamPath,
        IReadOnlyList<DownstreamTarget> downstreamTargets)
    {
        var errors = new List<ValidationError>();

        if (downstreamTargets.Count == 0)
        {
            errors.Add(new ValidationError(
                "NO_DOWNSTREAM_TARGETS",
                $"Route {routeId} has no downstream targets"));
        }

        if (downstreamTargets.Count > 10)
        {
            errors.Add(new ValidationError(
                "TOO_MANY_DOWNSTREAM_TARGETS",
                $"Route {routeId} has more than 10 downstream targets"));
        }

        return errors.AsReadOnly();
    }

    /// <summary>
    /// Validates global configuration consistency.
    /// </summary>
    /// <param name="ocelotVersion">The Ocelot version.</param>
    /// <param name="features">The features being used.</param>
    /// <returns>A list of validation errors.</returns>
    public IReadOnlyList<ValidationError> ValidateGlobalConfiguration(
        OcelotVersion ocelotVersion,
        IEnumerable<string> features)
    {
        var errors = new List<ValidationError>();
        var featureRegistry = GetFeatureRegistry();

        foreach (var feature in features.Distinct())
        {
            if (featureRegistry.TryGetValue(feature, out var minVersion))
            {
                if (!ocelotVersion.IsAtLeast(minVersion))
                {
                    errors.Add(new ValidationError(
                        "FEATURE_NOT_SUPPORTED",
                        $"Feature '{feature}' requires Ocelot {minVersion} or later, but current version is {ocelotVersion}"));
                }
            }
        }

        return errors.AsReadOnly();
    }

    private Dictionary<string, OcelotVersion> GetFeatureRegistry()
    {
        return new Dictionary<string, OcelotVersion>
        {
            ["grpc"] = OcelotVersion.V19_0,
            ["jwt"] = OcelotVersion.V18_0,
            ["rate-limit"] = OcelotVersion.V18_0,
            ["qos"] = OcelotVersion.V18_0,
            ["cache"] = OcelotVersion.V18_0,
            ["load-balancer"] = OcelotVersion.V18_0,
            ["websocket"] = OcelotVersion.V18_0,
            ["dynamic-routes"] = OcelotVersion.V20_0
        };
    }
}

public record ValidationError(string Code, string Message);