using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Application.UseCases.Route;
using DomainRoute = BitWrite.OcelotControl.Domain.Aggregates.Route.Route;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;

namespace BitWrite.OcelotControl.Application.UseCases.Route;

public class GetRouteQueryHandler
{
    private readonly IRouteRepository _routeRepository;

    public GetRouteQueryHandler(IRouteRepository routeRepository)
    {
        _routeRepository = routeRepository;
    }

    public async Task<RouteResponse?> HandleAsync(GetRouteQuery query, CancellationToken cancellationToken = default)
    {
        var route = await _routeRepository.GetAsync(query.Id, cancellationToken);
        return route != null ? MapToResponse(route) : null;
    }

    private static RouteResponse MapToResponse(DomainRoute route)
    {
        return new RouteResponse(
            route.Id,
            route.Key,
            route.Method,
            route.UpstreamPath,
            route.ServiceId,
            route.IsEnabled,
            route.DownstreamTargets,
            route.AuthenticationOptions,
            route.RateLimitOptions,
            route.QoSOptions,
            route.CacheOptions,
            route.LoadBalancerOptions,
            route.CreatedAt,
            route.UpdatedAt
        );
    }
}