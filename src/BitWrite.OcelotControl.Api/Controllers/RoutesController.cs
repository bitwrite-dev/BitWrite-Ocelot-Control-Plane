using ApiDtos = BitWrite.OcelotControl.Api.DTOs;
using AppRoute = BitWrite.OcelotControl.Application.UseCases.Route;
using DomainRoute = BitWrite.OcelotControl.Domain.Aggregates.Route.Route;
using DomainHttpMethod = BitWrite.OcelotControl.Domain.ValueObjects.Configuration.HttpMethod;
using DomainUpstreamPath = BitWrite.OcelotControl.Domain.ValueObjects.Configuration.UpstreamPath;
using DomainDownstreamTarget = BitWrite.OcelotControl.Domain.ValueObjects.Configuration.DownstreamTarget;
using DomainAuthenticationOptions = BitWrite.OcelotControl.Domain.ValueObjects.FeatureConfig.AuthenticationOptions;
using DomainAuthorizationOptions = BitWrite.OcelotControl.Domain.ValueObjects.FeatureConfig.AuthorizationOptions;
using DomainRateLimitOptions = BitWrite.OcelotControl.Domain.ValueObjects.FeatureConfig.RateLimitOptions;
using DomainQoSOptions = BitWrite.OcelotControl.Domain.ValueObjects.FeatureConfig.QoSOptions;
using DomainCacheOptions = BitWrite.OcelotControl.Domain.ValueObjects.FeatureConfig.CacheOptions;
using DomainLoadBalancerOptions = BitWrite.OcelotControl.Domain.ValueObjects.FeatureConfig.LoadBalancerOptions;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BitWrite.OcelotControl.Api.Controllers;

[ApiController]
[Route("api/v1/routes")]
public class RoutesController : BaseApiController
{
    private readonly AppRoute.CreateRouteCommandHandler _createRouteCommandHandler;
    private readonly AppRoute.GetRouteQueryHandler _getRouteQueryHandler;
    private readonly AppRoute.ListRoutesQueryHandler _listRoutesQueryHandler;
    private readonly AppRoute.UpdateRouteCommandHandler _updateRouteCommandHandler;
    private readonly AppRoute.EnableRouteCommandHandler _enableRouteCommandHandler;
    private readonly AppRoute.DisableRouteCommandHandler _disableRouteCommandHandler;
    private readonly AppRoute.DeleteRouteCommandHandler _deleteRouteCommandHandler;
    private readonly AppRoute.ValidateRouteCommandHandler _validateRouteCommandHandler;
    private readonly AppRoute.PreviewRouteQueryHandler _previewRouteQueryHandler;
    private readonly AppRoute.GetEffectiveRouteQueryHandler _getEffectiveRouteQueryHandler;
    private readonly AppRoute.RouteHistoryQueryHandler _routeHistoryQueryHandler;

    public RoutesController(
        AppRoute.CreateRouteCommandHandler createRouteCommandHandler,
        AppRoute.GetRouteQueryHandler getRouteQueryHandler,
        AppRoute.ListRoutesQueryHandler listRoutesQueryHandler,
        AppRoute.UpdateRouteCommandHandler updateRouteCommandHandler,
        AppRoute.EnableRouteCommandHandler enableRouteCommandHandler,
        AppRoute.DisableRouteCommandHandler disableRouteCommandHandler,
        AppRoute.DeleteRouteCommandHandler deleteRouteCommandHandler,
        AppRoute.ValidateRouteCommandHandler validateRouteCommandHandler,
        AppRoute.PreviewRouteQueryHandler previewRouteQueryHandler,
        AppRoute.GetEffectiveRouteQueryHandler getEffectiveRouteQueryHandler,
        AppRoute.RouteHistoryQueryHandler routeHistoryQueryHandler)
    {
        _createRouteCommandHandler = createRouteCommandHandler;
        _getRouteQueryHandler = getRouteQueryHandler;
        _listRoutesQueryHandler = listRoutesQueryHandler;
        _updateRouteCommandHandler = updateRouteCommandHandler;
        _enableRouteCommandHandler = enableRouteCommandHandler;
        _disableRouteCommandHandler = disableRouteCommandHandler;
        _deleteRouteCommandHandler = deleteRouteCommandHandler;
        _validateRouteCommandHandler = validateRouteCommandHandler;
        _previewRouteQueryHandler = previewRouteQueryHandler;
        _getEffectiveRouteQueryHandler = getEffectiveRouteQueryHandler;
        _routeHistoryQueryHandler = routeHistoryQueryHandler;
    }

    [HttpGet]
    public async Task<ActionResult<ApiDtos.RouteListResponse>> GetRoutes(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? serviceId = null,
        [FromQuery] bool? isEnabled = null,
        [FromQuery] string? search = null)
    {
        try
        {
            var query = new AppRoute.ListRoutesQuery(page, pageSize, serviceId, isEnabled, search);
            var result = await _listRoutesQueryHandler.HandleAsync(query);

            var response = new ApiDtos.RouteListResponse(
                result.Routes.Select(MapToResponse).ToList(),
                result.TotalCount,
                result.Page,
                result.PageSize
            );

            return HandleResult(response);
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiDtos.RouteResponse>> GetRoute(string id)
    {
        try
        {
            var query = new AppRoute.GetRouteQuery(RouteId.From(Guid.Parse(id)));
            var result = await _getRouteQueryHandler.HandleAsync(query);

            if (result == null)
                return NotFound();

            return HandleResult(MapToResponse(result));
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    [HttpPost]
    public async Task<ActionResult<ApiDtos.RouteResponse>> CreateRoute(ApiDtos.CreateRouteRequest request)
    {
        try
        {
            var command = new AppRoute.CreateRouteCommand(
                request.Key,
                DomainHttpMethod.Parse(request.Method),
                DomainUpstreamPath.From(request.UpstreamPath),
                ServiceId.From(Guid.Parse(request.ServiceId)),
                request.DownstreamTargets.Select(MapToDownstreamTarget).ToList(),
                request.Host,
                request.AuthenticationOptions != null ? DomainAuthenticationOptions.Create(
                    "Bearer",
                    null,
                    new Dictionary<string, string> { { "scopes", string.Join(",", request.AuthenticationOptions.AllowedScopes ?? new List<string>()) } }) : null,
                request.RateLimitOptions != null ? DomainRateLimitOptions.Create(
                    request.RateLimitOptions.Limit,
                    request.RateLimitOptions.Period) : null,
                request.QoSOptions != null ? DomainQoSOptions.Create(
                    request.QoSOptions.TimeoutSeconds,
                    circuitBreakerTimeoutSeconds: request.QoSOptions.CircuitBreakerTimeoutSeconds) : null,
                request.CacheOptions != null ? DomainCacheOptions.Create(request.CacheOptions.TtlSeconds) : null,
                request.LoadBalancerOptions != null ? DomainLoadBalancerOptions.Create(request.LoadBalancerOptions.Algorithm) : null,
                User.Identity?.Name ?? "system"
            );

            var result = await _createRouteCommandHandler.HandleAsync(command);
            return HandleResult(MapToResponse(result));
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ApiDtos.RouteResponse>> UpdateRoute(string id, ApiDtos.UpdateRouteRequest request)
    {
        try
        {
            var command = new AppRoute.UpdateRouteCommand(
                RouteId.From(Guid.Parse(id)),
                request.Key,
                request.Method != null ? DomainHttpMethod.Parse(request.Method) : null,
                request.UpstreamPath != null ? DomainUpstreamPath.From(request.UpstreamPath) : null,
                request.ServiceId != null ? ServiceId.From(Guid.Parse(request.ServiceId)) : null,
                request.DownstreamTargets != null ? request.DownstreamTargets.Select(MapToDownstreamTarget).ToList() : null,
                request.Host,
                request.AuthenticationOptions != null ? DomainAuthenticationOptions.Create(
                    "Bearer",
                    null,
                    new Dictionary<string, string> { { "scopes", string.Join(",", request.AuthenticationOptions.AllowedScopes ?? new List<string>()) } }) : null,
                request.RateLimitOptions != null ? DomainRateLimitOptions.Create(
                    request.RateLimitOptions.Limit,
                    request.RateLimitOptions.Period) : null,
                request.QoSOptions != null ? DomainQoSOptions.Create(
                    request.QoSOptions.TimeoutSeconds,
                    circuitBreakerTimeoutSeconds: request.QoSOptions.CircuitBreakerTimeoutSeconds) : null,
                request.CacheOptions != null ? DomainCacheOptions.Create(request.CacheOptions.TtlSeconds) : null,
                request.LoadBalancerOptions != null ? DomainLoadBalancerOptions.Create(request.LoadBalancerOptions.Algorithm) : null,
                User.Identity?.Name ?? "system"
            );

            var result = await _updateRouteCommandHandler.HandleAsync(command);
            return HandleResult(MapToResponse(result));
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    [HttpPatch("{id}/enable")]
    public async Task<ActionResult<ApiDtos.RouteResponse>> EnableRoute(string id)
    {
        try
        {
            var command = new AppRoute.EnableRouteCommand(
                RouteId.From(Guid.Parse(id)),
                User.Identity?.Name ?? "system"
            );

            var result = await _enableRouteCommandHandler.HandleAsync(command);
            return HandleResult(MapToResponse(result));
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    [HttpPatch("{id}/disable")]
    public async Task<ActionResult<ApiDtos.RouteResponse>> DisableRoute(string id)
    {
        try
        {
            var command = new AppRoute.DisableRouteCommand(
                RouteId.From(Guid.Parse(id)),
                User.Identity?.Name ?? "system"
            );

            var result = await _disableRouteCommandHandler.HandleAsync(command);
            return HandleResult(MapToResponse(result));
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteRoute(string id)
    {
        try
        {
            var command = new AppRoute.DeleteRouteCommand(
                RouteId.From(Guid.Parse(id)),
                User.Identity?.Name ?? "system"
            );

            await _deleteRouteCommandHandler.HandleAsync(command);
            return NoContent();
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    [HttpPost("{id}/validate")]
    public async Task<ActionResult<ApiDtos.RouteValidationResponse>> ValidateRoute(string id)
    {
        try
        {
            var command = new AppRoute.ValidateRouteCommand(
                RouteId.From(Guid.Parse(id)),
                User.Identity?.Name ?? "system"
            );

            var result = await _validateRouteCommandHandler.HandleAsync(command);
            return HandleResult(new ApiDtos.RouteValidationResponse(result.IsValid, result.Errors.ToList()));
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    [HttpGet("{id}/preview")]
    public async Task<ActionResult<ApiDtos.RoutePreviewResponse>> PreviewRoute(string id)
    {
        try
        {
            var query = new AppRoute.PreviewRouteQuery(RouteId.From(Guid.Parse(id)));
            var result = await _previewRouteQueryHandler.HandleAsync(query);
            return HandleResult(new ApiDtos.RoutePreviewResponse(result.OcelotJson));
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    [HttpGet("{id}/effective")]
    public async Task<ActionResult<ApiDtos.RouteEffectiveResponse>> GetEffectiveRoute(string id)
    {
        try
        {
            var query = new AppRoute.GetEffectiveRouteQuery(RouteId.From(Guid.Parse(id)));
            var result = await _getEffectiveRouteQueryHandler.HandleAsync(query);
            return HandleResult(new ApiDtos.RouteEffectiveResponse(result.OcelotJson));
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    [HttpGet("{id}/history")]
    public async Task<ActionResult<ApiDtos.RouteHistoryResponse>> GetRouteHistory(string id)
    {
        try
        {
            var query = new AppRoute.RouteHistoryQuery(RouteId.From(Guid.Parse(id)));
            var result = await _routeHistoryQueryHandler.HandleAsync(query);
            return HandleResult(new ApiDtos.RouteHistoryResponse(result.History.Select(MapToHistoryItem).ToList()));
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    private static DomainDownstreamTarget MapToDownstreamTarget(ApiDtos.DownstreamTargetRequest request)
    {
        return DomainDownstreamTarget.Create(request.Scheme, request.Host, request.Port, request.Path);
    }

    private static ApiDtos.RouteResponse MapToResponse(AppRoute.RouteResponse route)
    {
        // Extract host from RouteKey if available
        var routeKey = route.Key;
        var host = routeKey.Contains("://") ? "" : ""; // Simplified - route.Key might include host info

        return new ApiDtos.RouteResponse(
            route.Id.Value.ToString(),
            route.Key,
            route.Method.Value,
            route.UpstreamPath.Value,
            host, // Route aggregate doesn't expose Host directly in response
            route.ServiceId.Value.ToString(),
            route.IsEnabled,
            route.DownstreamTargets.Select(t => new ApiDtos.DownstreamTargetResponse(t.Host, t.Port, t.Scheme, t.Path)).ToList(),
            route.AuthenticationOptions != null ? new ApiDtos.AuthenticationOptionsResponse(
                route.AuthenticationOptions.Properties?.TryGetValue("scopes", out var scopes) == true
                    ? scopes.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList()
                    : new List<string>()) : null,
            route.RateLimitOptions != null ? new ApiDtos.RateLimitOptionsResponse(
                true, // EnableRateLimiting is true if RateLimitOptions exists
                route.RateLimitOptions.Period ?? "Second",
                route.RateLimitOptions.Limit ?? 0) : null,
            route.QoSOptions != null ? new ApiDtos.QoSOptionsResponse(
                route.QoSOptions.TimeoutSeconds ?? 0,
                route.QoSOptions.CircuitBreakerTimeoutSeconds) : null,
            route.CacheOptions != null ? new ApiDtos.CacheOptionsResponse(route.CacheOptions.TtlSeconds) : null,
            route.LoadBalancerOptions != null ? new ApiDtos.LoadBalancerOptionsResponse(route.LoadBalancerOptions.Algorithm) : null,
            route.CreatedAt,
            route.UpdatedAt
        );
    }

    private static ApiDtos.RouteHistoryItem MapToHistoryItem(AppRoute.RouteHistoryItem item)
    {
        return new ApiDtos.RouteHistoryItem(item.Timestamp, item.Action, item.ChangedBy, item.Details);
    }
}