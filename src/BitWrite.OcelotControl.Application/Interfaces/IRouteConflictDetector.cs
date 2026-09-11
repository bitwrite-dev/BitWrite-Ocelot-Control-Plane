using BitWrite.OcelotControl.Domain.Aggregates.Route;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;

namespace BitWrite.OcelotControl.Application.Interfaces;

public interface IRouteConflictDetector
{
    IReadOnlyList<RouteKey> DetectConflicts(
        RouteKey newRouteKey,
        IEnumerable<(RouteId Id, RouteKey Key)> existingRouteKeys,
        RouteId? excludeRouteId = null);
}