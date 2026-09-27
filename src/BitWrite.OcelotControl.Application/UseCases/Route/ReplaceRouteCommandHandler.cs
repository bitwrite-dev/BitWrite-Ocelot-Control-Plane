using BitWrite.OcelotControl.Application.Interfaces;
using DomainRoute = BitWrite.OcelotControl.Domain.Aggregates.Route.Route;
using BitWrite.OcelotControl.Domain.Events;

namespace BitWrite.OcelotControl.Application.UseCases.Route;

/// <summary>
/// Replaces the configuration of a stored route.
/// </summary>
/// <remarks>
/// Applies every field unconditionally, so the persisted route ends up holding
/// exactly what the command carries. The previous handler skipped method,
/// upstream path and downstream targets outright and treated a null everywhere
/// as "leave unchanged", which meant a retargeted route reported success and
/// kept sending traffic to the old targets.
/// </remarks>
public class ReplaceRouteCommandHandler
{
    private readonly IRouteRepository _routeRepository;
    private readonly IServiceRepository _serviceRepository;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public ReplaceRouteCommandHandler(
        IRouteRepository routeRepository,
        IServiceRepository serviceRepository,
        IDomainEventDispatcher eventDispatcher)
    {
        _routeRepository = routeRepository;
        _serviceRepository = serviceRepository;
        _eventDispatcher = eventDispatcher;
    }

    public async Task<RouteResponse?> HandleAsync(
        ReplaceRouteCommand command,
        CancellationToken cancellationToken = default)
    {
        var route = await _routeRepository.GetAsync(command.Id, cancellationToken);
        if (route == null)
            return null;

        // Checked before the write, so a route is never pointed at a service
        // that does not exist.
        if (command.ServiceId != route.ServiceId)
        {
            var service = await _serviceRepository.GetAsync(command.ServiceId, cancellationToken);
            if (service == null)
                throw new InvalidOperationException($"Service {command.ServiceId} not found");
        }

        route.Replace(
            command.Method,
            command.UpstreamPath,
            command.ServiceId,
            command.DownstreamTargets,
            command.Key,
            command.Host,
            command.AuthenticationOptions,
            command.RateLimitOptions,
            command.QoSOptions,
            command.CacheOptions,
            command.LoadBalancerOptions);

        await _routeRepository.UpdateAsync(route, cancellationToken);

        foreach (var domainEvent in route.DomainEvents)
        {
            await _eventDispatcher.DispatchAsync(domainEvent, cancellationToken);
        }
        route.ClearDomainEvents();

        await _eventDispatcher.DispatchAsync(
            new AuditRecorded(
                command.InitiatedBy,
                "ReplaceRoute",
                "Route",
                route.Id.Value.ToString(),
                "Success"),
            cancellationToken);

        return MapToResponse(route);
    }

    /// <summary>
    /// Projects the aggregate onto the response shape.
    /// </summary>
    /// <remarks>
    /// The option blocks are passed through as the domain holds them, which is
    /// how the create handler reports them too.
    /// </remarks>
    private static RouteResponse MapToResponse(DomainRoute route) =>
        new(
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
            route.UpdatedAt);
}
