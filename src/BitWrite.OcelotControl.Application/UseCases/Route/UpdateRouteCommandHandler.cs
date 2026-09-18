using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Application.UseCases.Route;
using DomainRoute = BitWrite.OcelotControl.Domain.Aggregates.Route.Route;
using BitWrite.OcelotControl.Domain.Events;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Domain.ValueObjects.FeatureConfig;

namespace BitWrite.OcelotControl.Application.UseCases.Route;

public class UpdateRouteCommandHandler
{
    private readonly IRouteRepository _routeRepository;
    private readonly IServiceRepository _serviceRepository;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public UpdateRouteCommandHandler(
        IRouteRepository routeRepository,
        IServiceRepository serviceRepository,
        IDomainEventDispatcher eventDispatcher)
    {
        _routeRepository = routeRepository;
        _serviceRepository = serviceRepository;
        _eventDispatcher = eventDispatcher;
    }

    public async Task<RouteResponse?> HandleAsync(UpdateRouteCommand command, CancellationToken cancellationToken = default)
    {
        var route = await _routeRepository.GetAsync(command.Id, cancellationToken);
        if (route == null)
            return null;

        // Update ServiceId if provided
        if (command.ServiceId != null && command.ServiceId != route.ServiceId)
        {
            var service = await _serviceRepository.GetAsync(command.ServiceId, cancellationToken);
            if (service == null)
            {
                throw new InvalidOperationException($"Service {command.ServiceId} not found");
            }
            // Route aggregate doesn't have UpdateServiceId method yet
            // This would need to be added to the aggregate
        }

        // Update key
        if (command.Key != null)
            route.UpdateKey(command.Key);

        // Update host
        if (command.Host != null)
            route.UpdateHost(command.Host);

        // Update method - would need UpdateMethod on aggregate
        if (command.Method != null)
        {
            // Skip for now
        }

        // Update upstream path - would need UpdateUpstreamPath on aggregate
        if (command.UpstreamPath != null)
        {
            // Skip for now
        }

        // Update downstream targets - would need ClearDownstreamTargets on aggregate
        if (command.DownstreamTargets != null)
        {
            // Skip for now
        }

        // Update feature configurations
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
            "UpdateRoute",
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