using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Application.UseCases.Route;
using BitWrite.OcelotControl.Domain.Aggregates.Route;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;

namespace BitWrite.OcelotControl.Application.UseCases.Route;

public class RouteHistoryQueryHandler
{
    private readonly IRouteRepository _routeRepository;

    public RouteHistoryQueryHandler(IRouteRepository routeRepository)
    {
        _routeRepository = routeRepository;
    }

    public async Task<RouteHistoryResponse?> HandleAsync(RouteHistoryQuery query, CancellationToken cancellationToken = default)
    {
        var route = await _routeRepository.GetAsync(query.Id, cancellationToken);
        if (route == null)
            return null;

        // TODO: Implement actual history from audit log or event store
        // For now, return basic history from the route's current state
        var history = new List<RouteHistoryItem>
        {
            new RouteHistoryItem(
                route.CreatedAt,
                "Created",
                null,
                $"Route created with key: {route.Key}"
            ),
            new RouteHistoryItem(
                route.UpdatedAt,
                route.IsEnabled ? "Enabled" : "Disabled",
                null,
                $"Route status: {(route.IsEnabled ? "Enabled" : "Disabled")}"
            )
        };

        return new RouteHistoryResponse(history);
    }
}