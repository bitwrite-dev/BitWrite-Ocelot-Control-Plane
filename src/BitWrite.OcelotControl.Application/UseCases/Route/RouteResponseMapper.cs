using DomainRoute = BitWrite.OcelotControl.Domain.Aggregates.Route.Route;

namespace BitWrite.OcelotControl.Application.UseCases.Route;

/// <summary>
/// The single place a route aggregate is projected onto the response shape.
/// </summary>
/// <remarks>
/// Five handlers used to carry their own copy of this mapping, which is how the
/// option blocks drifted: adding one meant editing every copy, and a copy that
/// was missed reports the option as absent while the route still has it.
/// </remarks>
public static class RouteResponseMapper
{
    public static RouteResponse Map(DomainRoute route) =>
        new(
            route.Id,
            route.Key,
            route.Method,
            route.UpstreamPath,
            route.ServiceId,
            route.IsEnabled,
            route.DownstreamTargets,
            route.AuthenticationOptions,
            route.AuthorizationOptions,
            route.RateLimitOptions,
            route.QoSOptions,
            route.CacheOptions,
            route.LoadBalancerOptions,
            route.HeaderOptions,
            route.ClaimOptions,
            route.QueryOptions,
            route.CreatedAt,
            route.UpdatedAt);
}
