using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Domain.Services;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using ValidationError = BitWrite.OcelotControl.Domain.Services.ValidationError;

namespace BitWrite.OcelotControl.Infrastructure.Adapters;

/// <summary>
/// Adapter that implements IConfigurationConsistencyValidator using Domain.ConfigurationConsistencyValidator.
/// </summary>
public class ConfigurationConsistencyValidatorAdapter : IConfigurationConsistencyValidator
{
    private readonly ConfigurationConsistencyValidator _domainValidator;

    public ConfigurationConsistencyValidatorAdapter(ConfigurationConsistencyValidator domainValidator)
    {
        _domainValidator = domainValidator;
    }

    public IReadOnlyList<ValidationError> ValidateServiceReferences(
        IEnumerable<ServiceId> routeServiceIds,
        IEnumerable<ServiceId> existingServiceIds)
    {
        return _domainValidator.ValidateServiceReferences(routeServiceIds, existingServiceIds);
    }

    public IReadOnlyList<ValidationError> ValidateDownstreamTargets(
        IEnumerable<DownstreamTarget> targets)
    {
        return _domainValidator.ValidateDownstreamTargets(targets);
    }

    public IReadOnlyList<ValidationError> ValidateRouteConfiguration(
        RouteId routeId,
        ServiceId serviceId,
        UpstreamPath upstreamPath,
        IReadOnlyList<DownstreamTarget> downstreamTargets)
    {
        return _domainValidator.ValidateRouteConfiguration(routeId, serviceId, upstreamPath, downstreamTargets);
    }

    public IReadOnlyList<ValidationError> ValidateGlobalConfiguration(
        OcelotVersion ocelotVersion,
        IEnumerable<string> features)
    {
        return _domainValidator.ValidateGlobalConfiguration(ocelotVersion, features);
    }
}