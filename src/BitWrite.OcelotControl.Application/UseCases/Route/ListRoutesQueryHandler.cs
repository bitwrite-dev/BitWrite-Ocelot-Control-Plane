using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Application.UseCases.Route;
using DomainRoute = BitWrite.OcelotControl.Domain.Aggregates.Route.Route;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;

namespace BitWrite.OcelotControl.Application.UseCases.Route;

public class ListRoutesQueryHandler
{
    private readonly IRouteRepository _routeRepository;

    public ListRoutesQueryHandler(IRouteRepository routeRepository)
    {
        _routeRepository = routeRepository;
    }

    public async Task<RouteListResponse> HandleAsync(ListRoutesQuery query, CancellationToken cancellationToken = default)
    {
        var routes = await _routeRepository.GetAllAsync(cancellationToken);

        // Apply filters
        if (!string.IsNullOrEmpty(query.ServiceId) && Guid.TryParse(query.ServiceId, out var serviceIdGuid))
        {
            var serviceId = ServiceId.From(serviceIdGuid);
            routes = routes.Where(r => r.ServiceId == serviceId).ToList();
        }

        if (query.IsEnabled.HasValue)
        {
            routes = routes.Where(r => r.IsEnabled == query.IsEnabled.Value).ToList();
        }

        if (!string.IsNullOrEmpty(query.Search))
        {
            var search = query.Search.ToLowerInvariant();
            routes = routes.Where(r => 
                (r.Key?.ToLowerInvariant().Contains(search) == true) ||
                (r.Host?.ToLowerInvariant().Contains(search) == true) ||
                r.UpstreamPath.Value.ToLowerInvariant().Contains(search) ||
                r.ServiceId.Value.ToString().Contains(search)
            ).ToList();
        }

        var totalCount = routes.Count;
        var pagedRoutes = routes
            .OrderBy(r => r.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();

        var response = new RouteListResponse(
            pagedRoutes.Select(MapToResponse).ToList(),
            totalCount,
            query.Page,
            query.PageSize
        );

        return response;
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