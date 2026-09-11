using BitWrite.OcelotControl.Domain.Aggregates.Route;
using BitWrite.OcelotControl.Domain.Aggregates.Service;
using BitWrite.OcelotControl.Domain.Services;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Domain.ValueObjects.Status;
using ValidationError = BitWrite.OcelotControl.Domain.Services.ValidationError;

namespace BitWrite.OcelotControl.Application.Interfaces;

public interface IConfigurationConsistencyValidator
{
    IReadOnlyList<ValidationError> ValidateServiceReferences(
        IEnumerable<ServiceId> routeServiceIds,
        IEnumerable<ServiceId> existingServiceIds);

    IReadOnlyList<ValidationError> ValidateDownstreamTargets(
        IEnumerable<DownstreamTarget> targets);

    IReadOnlyList<ValidationError> ValidateRouteConfiguration(
        RouteId routeId,
        ServiceId serviceId,
        UpstreamPath upstreamPath,
        IReadOnlyList<DownstreamTarget> downstreamTargets);

    IReadOnlyList<ValidationError> ValidateGlobalConfiguration(
        OcelotVersion ocelotVersion,
        IEnumerable<string> features);
}