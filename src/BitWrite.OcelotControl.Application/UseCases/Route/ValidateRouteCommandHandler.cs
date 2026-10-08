using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Application.UseCases.Route;
using BitWrite.OcelotControl.Domain.Services;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using DomainRoute = BitWrite.OcelotControl.Domain.Aggregates.Route.Route;

namespace BitWrite.OcelotControl.Application.UseCases.Route;

/// <summary>
/// Validates a route that is already stored.
/// </summary>
/// <remarks>
/// Loads the aggregate and hands it to the shared <see cref="RouteValidator"/>,
/// so this endpoint and the draft endpoint cannot drift apart. Kept as a
/// separate handler because only this one has to read from the repository.
/// </remarks>
public class ValidateRouteCommandHandler
{
    private readonly IRouteRepository _routeRepository;
    private readonly RouteValidator _validator;

    public ValidateRouteCommandHandler(
        IRouteRepository routeRepository,
        RouteValidator validator)
    {
        _routeRepository = routeRepository;
        _validator = validator;
    }

    public async Task<RouteValidationResult> HandleAsync(
        ValidateRouteCommand command,
        CancellationToken cancellationToken = default)
    {
        var route = await _routeRepository.GetAsync(command.Id, cancellationToken);
        if (route == null)
        {
            return new RouteValidationResult(
                false,
                new[] { new RouteValidationError(null, "ROUTE_NOT_FOUND", $"Route {command.Id} not found") });
        }

        return await _validator.ValidateAsync(ToInput(route), cancellationToken);
    }

    /// <summary>
    /// Projects a stored route onto the validator's input.
    /// </summary>
    /// <remarks>
    /// This copy was missing authorization and the transformation blocks, so a
    /// route using them was validated against a configuration that did not
    /// contain them. It now shares the one mapping.
    /// </remarks>
    internal static RouteValidationInput ToInput(DomainRoute route) => new(
        RouteConfigurationMapper.Map(route),
        route.RouteKey,
        // A stored route must not conflict with itself.
        route.Id,
        FeaturesOf(route));

    /// <summary>
    /// The capabilities a stored route has switched on.
    /// </summary>
    internal static IReadOnlyList<string> FeaturesOf(DomainRoute route)
    {
        var features = new List<string>();

        if (route.AuthenticationOptions != null) features.Add("authentication");
        if (route.AuthorizationOptions != null) features.Add("authorization");
        if (route.RateLimitOptions != null) features.Add("rate-limiting");
        if (route.QoSOptions != null) features.Add("qos");
        if (route.CacheOptions != null) features.Add("caching");
        if (route.LoadBalancerOptions != null) features.Add("load-balancing");
        if (route.HeaderOptions != null) features.Add("header-transformation");
        if (route.ClaimOptions != null) features.Add("claim-transformation");
        if (route.QueryOptions != null) features.Add("query-string-transformation");

        return features;
    }
}
