using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Application.UseCases.Route;
using DomainRoute = BitWrite.OcelotControl.Domain.Aggregates.Route.Route;
using BitWrite.OcelotControl.Domain.Events;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;

namespace BitWrite.OcelotControl.Application.UseCases.Route;

public class CreateRouteCommandHandler
{
    private readonly IRouteRepository _routeRepository;
    private readonly IServiceRepository _serviceRepository;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public CreateRouteCommandHandler(
        IRouteRepository routeRepository,
        IServiceRepository serviceRepository,
        IDomainEventDispatcher eventDispatcher)
    {
        _routeRepository = routeRepository;
        _serviceRepository = serviceRepository;
        _eventDispatcher = eventDispatcher;
    }

    public async Task<RouteResponse> HandleAsync(CreateRouteCommand command, CancellationToken cancellationToken = default)
    {
        // Validate service exists
        var service = await _serviceRepository.GetAsync(command.ServiceId, cancellationToken);
        if (service == null)
        {
            throw new InvalidOperationException($"Service {command.ServiceId} not found");
        }

        // Create Route aggregate
        var route = DomainRoute.Create(
            command.Method,
            command.UpstreamPath,
            command.ServiceId,
            command.DownstreamTargets,
            command.Key,
            command.Host
        );

        // Set optional feature configurations
        if (command.AuthenticationOptions != null)
            route.SetAuthentication(command.AuthenticationOptions);
        if (command.RateLimitOptions != null)
            route.SetRateLimit(command.RateLimitOptions);
        if (command.QoSOptions != null)
            route.SetQoS(command.QoSOptions);
        if (command.CacheOptions != null)
            route.SetCache(command.CacheOptions);
        if (command.LoadBalancerOptions != null)
            route.SetLoadBalancer(command.LoadBalancerOptions);

        // Persist
        await _routeRepository.AddAsync(route, cancellationToken);

        // Dispatch domain events
        foreach (var domainEvent in route.DomainEvents)
        {
            await _eventDispatcher.DispatchAsync(domainEvent, cancellationToken);
        }
        route.ClearDomainEvents();

        // Dispatch audit event
        var auditEvent = new AuditRecorded(
            command.InitiatedBy,
            "CreateRoute",
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