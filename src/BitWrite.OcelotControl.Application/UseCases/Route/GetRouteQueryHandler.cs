using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Application.UseCases.Route;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using DomainRoute = BitWrite.OcelotControl.Domain.Aggregates.Route.Route;

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
        return route != null ? RouteResponseMapper.Map(route) : null;
    }

}
