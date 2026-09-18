using BitWrite.OcelotControl.Application.Interfaces;

namespace BitWrite.OcelotControl.Application.UseCases.Route;

public class GetRouteStatusCommandHandler
{
    private readonly IRouteRepository _routeRepository;

    public GetRouteStatusCommandHandler(IRouteRepository routeRepository)
    {
        _routeRepository = routeRepository;
    }

    public async Task<RouteStatusResponse?> HandleAsync(GetRouteStatusCommand command, CancellationToken cancellationToken = default)
    {
        var route = await _routeRepository.GetAsync(command.RouteId, cancellationToken);
        if (route == null)
            return null;

        return new RouteStatusResponse(
            route.Id,
            route.Key ?? "",
            route.IsEnabled,
            route.UpdatedAt);
    }
}
