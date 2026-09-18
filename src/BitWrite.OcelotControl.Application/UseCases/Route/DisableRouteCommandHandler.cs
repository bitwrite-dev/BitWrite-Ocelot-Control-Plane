using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Application.UseCases.Route;
using DomainRoute = BitWrite.OcelotControl.Domain.Aggregates.Route.Route;
using BitWrite.OcelotControl.Domain.Events;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;

namespace BitWrite.OcelotControl.Application.UseCases.Route;

public class DisableRouteCommandHandler
{
    private readonly IRouteRepository _routeRepository;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public DisableRouteCommandHandler(
        IRouteRepository routeRepository,
        IDomainEventDispatcher eventDispatcher)
    {
        _routeRepository = routeRepository;
        _eventDispatcher = eventDispatcher;
    }

    public async Task<RouteResponse?> HandleAsync(DisableRouteCommand command, CancellationToken cancellationToken = default)
    {
        var route = await _routeRepository.GetAsync(command.Id, cancellationToken);
        if (route == null)
            return null;

        // Disable the route
        route.Disable();

        // Persist
        await _routeRepository.UpdateAsync(route, cancellationToken);

        // Dispatch domain events
        foreach (var domainEvent in route.DomainEvents)
        {
            await _eventDispatcher.DispatchAsync(domainEvent, cancellationToken);
        }
        route.ClearDomainEvents();

        // Dispatch audit event
        var auditEvent = new AuditRecorded(
            command.InitiatedBy,
            "DisableRoute",
            "Route",
            route.Id.Value.ToString(),
            "Success"
        );
        await _eventDispatcher.DispatchAsync(auditEvent, cancellationToken);

        return MapToResponse(route);
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