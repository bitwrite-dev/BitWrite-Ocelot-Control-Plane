using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Application.UseCases.Service;
using DomainService = BitWrite.OcelotControl.Domain.Aggregates.Service.Service;
using DomainRoute = BitWrite.OcelotControl.Domain.Aggregates.Route.Route;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;

namespace BitWrite.OcelotControl.Application.UseCases.Service;

public class GetServiceRoutesQueryHandler
{
    private readonly IServiceRepository _serviceRepository;
    private readonly IRouteRepository _routeRepository;

    public GetServiceRoutesQueryHandler(
        IServiceRepository serviceRepository,
        IRouteRepository routeRepository)
    {
        _serviceRepository = serviceRepository;
        _routeRepository = routeRepository;
    }

    public async Task<ServiceRoutesResponse?> HandleAsync(GetServiceRoutesQuery query, CancellationToken cancellationToken = default)
    {
        var service = await _serviceRepository.GetAsync(query.Id, cancellationToken);
        if (service == null)
            return null;

        // Get all routes for this service
        var allRoutes = await _routeRepository.GetAllAsync(cancellationToken);
        var serviceRoutes = allRoutes.Where(r => r.ServiceId == query.Id).ToList();

        var response = new ServiceRoutesResponse(
            serviceRoutes.Select(r => new RouteSummary(
                r.Id,
                r.Key,
                r.Method.Value,
                r.UpstreamPath.Value,
                r.IsEnabled
            )).ToList()
        );

        return response;
    }
}