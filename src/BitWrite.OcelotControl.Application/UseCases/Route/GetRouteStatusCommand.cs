using BitWrite.OcelotControl.Domain.ValueObjects.Identity;

namespace BitWrite.OcelotControl.Application.UseCases.Route;

public record GetRouteStatusCommand(
    RouteId RouteId
);

public record RouteStatusResponse(
    RouteId Id,
    string Key,
    bool IsEnabled,
    DateTimeOffset LastUpdatedAt
);
