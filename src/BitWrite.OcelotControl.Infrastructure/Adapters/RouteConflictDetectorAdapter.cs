using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Domain.Services;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;

namespace BitWrite.OcelotControl.Infrastructure.Adapters;

/// <summary>
/// Adapter that implements IRouteConflictDetector using Domain.RouteConflictDetector.
/// </summary>
public class RouteConflictDetectorAdapter : IRouteConflictDetector
{
    private readonly RouteConflictDetector _domainDetector;

    public RouteConflictDetectorAdapter(RouteConflictDetector domainDetector)
    {
        _domainDetector = domainDetector;
    }

    public IReadOnlyList<RouteKey> DetectConflicts(
        RouteKey newRouteKey,
        IEnumerable<(RouteId Id, RouteKey Key)> existingRouteKeys,
        RouteId? excludeRouteId = null)
    {
        return _domainDetector.DetectConflicts(newRouteKey, existingRouteKeys, excludeRouteId);
    }
}