using BitWrite.OcelotControl.Api.Mapping;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using ApiDtos = BitWrite.OcelotControl.Api.DTOs;
using AppRoute = BitWrite.OcelotControl.Application.UseCases.Route;
using DomainAuthenticationOptions = BitWrite.OcelotControl.Domain.ValueObjects.FeatureConfig.AuthenticationOptions;
using DomainAuthorizationOptions = BitWrite.OcelotControl.Domain.ValueObjects.FeatureConfig.AuthorizationOptions;
using DomainCacheOptions = BitWrite.OcelotControl.Domain.ValueObjects.FeatureConfig.CacheOptions;
using DomainClaimOptions = BitWrite.OcelotControl.Domain.ValueObjects.FeatureConfig.ClaimOptions;
using DomainDownstreamTarget = BitWrite.OcelotControl.Domain.ValueObjects.Configuration.DownstreamTarget;
using DomainHeaderOptions = BitWrite.OcelotControl.Domain.ValueObjects.FeatureConfig.HeaderOptions;
using DomainHttpMethod = BitWrite.OcelotControl.Domain.ValueObjects.Configuration.HttpMethod;
using DomainLoadBalancerOptions = BitWrite.OcelotControl.Domain.ValueObjects.FeatureConfig.LoadBalancerOptions;
using DomainQoSOptions = BitWrite.OcelotControl.Domain.ValueObjects.FeatureConfig.QoSOptions;
using DomainQueryOptions = BitWrite.OcelotControl.Domain.ValueObjects.FeatureConfig.QueryOptions;
using DomainRateLimitOptions = BitWrite.OcelotControl.Domain.ValueObjects.FeatureConfig.RateLimitOptions;
using DomainRoute = BitWrite.OcelotControl.Domain.Aggregates.Route.Route;
using DomainUpstreamPath = BitWrite.OcelotControl.Domain.ValueObjects.Configuration.UpstreamPath;

namespace BitWrite.OcelotControl.Api.Controllers;

[ApiController]
[Route("api/v1/routes")]
public class RoutesController : BaseApiController
{
    private readonly AppRoute.CreateRouteCommandHandler _createRouteCommandHandler;
    private readonly AppRoute.GetRouteQueryHandler _getRouteQueryHandler;
    private readonly AppRoute.ListRoutesQueryHandler _listRoutesQueryHandler;
    private readonly AppRoute.ReplaceRouteCommandHandler _replaceRouteCommandHandler;
    private readonly AppRoute.EnableRouteCommandHandler _enableRouteCommandHandler;
    private readonly AppRoute.DisableRouteCommandHandler _disableRouteCommandHandler;
    private readonly AppRoute.DeleteRouteCommandHandler _deleteRouteCommandHandler;
    private readonly AppRoute.ValidateRouteCommandHandler _validateRouteCommandHandler;
    private readonly AppRoute.ValidateRouteDraftCommandHandler _validateRouteDraftCommandHandler;
    private readonly AppRoute.PreviewRouteQueryHandler _previewRouteQueryHandler;
    private readonly AppRoute.GetEffectiveRouteQueryHandler _getEffectiveRouteQueryHandler;
    private readonly AppRoute.RouteHistoryQueryHandler _routeHistoryQueryHandler;

    public RoutesController(
        AppRoute.CreateRouteCommandHandler createRouteCommandHandler,
        AppRoute.GetRouteQueryHandler getRouteQueryHandler,
        AppRoute.ListRoutesQueryHandler listRoutesQueryHandler,
        AppRoute.ReplaceRouteCommandHandler replaceRouteCommandHandler,
        AppRoute.EnableRouteCommandHandler enableRouteCommandHandler,
        AppRoute.DisableRouteCommandHandler disableRouteCommandHandler,
        AppRoute.DeleteRouteCommandHandler deleteRouteCommandHandler,
        AppRoute.ValidateRouteCommandHandler validateRouteCommandHandler,
        AppRoute.ValidateRouteDraftCommandHandler validateRouteDraftCommandHandler,
        AppRoute.PreviewRouteQueryHandler previewRouteQueryHandler,
        AppRoute.GetEffectiveRouteQueryHandler getEffectiveRouteQueryHandler,
        AppRoute.RouteHistoryQueryHandler routeHistoryQueryHandler)
    {
        _createRouteCommandHandler = createRouteCommandHandler;
        _getRouteQueryHandler = getRouteQueryHandler;
        _listRoutesQueryHandler = listRoutesQueryHandler;
        _replaceRouteCommandHandler = replaceRouteCommandHandler;
        _enableRouteCommandHandler = enableRouteCommandHandler;
        _disableRouteCommandHandler = disableRouteCommandHandler;
        _deleteRouteCommandHandler = deleteRouteCommandHandler;
        _validateRouteCommandHandler = validateRouteCommandHandler;
        _validateRouteDraftCommandHandler = validateRouteDraftCommandHandler;
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
            var query = new AppRoute.GetRouteQuery(RouteId.From(id));
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

    /// <summary>
    /// Validates a route that has not been saved.
    /// </summary>
    /// <remarks>
    /// Lets the wizard check a draft before offering to save it. A body that
    /// cannot even be mapped reports the offending field with 200 and
    /// <c>isValid: false</c>, because a malformed draft is the expected input
    /// here rather than a client mistake.
    /// </remarks>
    [HttpPost("validate")]
    public async Task<ActionResult<ApiDtos.RouteValidationResponse>> ValidateRouteDraft(
        [FromBody] ApiDtos.CreateRouteRequest request)
    {
        var mapping = RouteRequestMapper.ToValidationInput(request);
        if (!mapping.Success)
        {
            return Ok(new ApiDtos.RouteValidationResponse(
                false,
                mapping.Errors.Select(ToValidationErrorResponse).ToList()));
        }

        var result = await _validateRouteDraftCommandHandler.HandleAsync(
            new AppRoute.ValidateRouteDraftCommand(
                mapping.Value!,
                User.Identity?.Name ?? "system"));

        return Ok(ToValidationResponse(result));
    }

    [HttpPost]
    public async Task<ActionResult<ApiDtos.RouteResponse>> CreateRoute(ApiDtos.CreateRouteRequest request)
    {
        try
        {
            // Same mapper the draft endpoint uses, so the two cannot disagree.
            var mapping = RouteRequestMapper.ToCreateCommand(
                request,
                User.Identity?.Name ?? "system");

            if (!mapping.Success)
            {
                return BadRequest(FieldErrors(mapping.Errors));
            }

            var result = await _createRouteCommandHandler.HandleAsync(mapping.Value!);
            return HandleResult(MapToResponse(result));
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    /// <summary>
    /// Turns field-tagged errors into the model state a
    /// <see cref="ValidationProblemDetails"/> is built from, so the response
    /// shape stays the framework's rather than a bespoke one.
    /// </summary>
    private static ModelStateDictionary ToModelState(IEnumerable<AppRoute.RouteValidationError> errors)
    {
        var state = new ModelStateDictionary();

        foreach (var group in errors.GroupBy(error => error.Field ?? string.Empty))
        {
            var key = string.IsNullOrEmpty(group.Key) ? "request" : group.Key;
            foreach (var error in group)
            {
                state.AddModelError(key, error.Message);
            }
        }

        return state;
    }

    /// <summary>
    /// A <see cref="ValidationProblemDetails"/> naming the field at fault.
    /// </summary>
    private ActionResult FieldErrors(IEnumerable<AppRoute.RouteValidationError> errors)
    {
        var problem = new ValidationProblemDetails(ToModelState(errors))
        {
            Status = 400,
            Title = "One or more validation errors occurred.",
        };

        // ValidationProblemDetails has no correlation id of its own, so it is
        // added as an extension property; the dashboard reads it to show in
        // error reports.
        problem.Extensions["correlationId"] = CorrelationId;
        return BadRequest(problem);
    }

    private static ApiDtos.RouteValidationResponse ToValidationResponse(
        AppRoute.RouteValidationResult result) =>
        new(
            result.IsValid,
            result.Errors.Select(ToValidationErrorResponse).ToList());

    private static ApiDtos.RouteValidationErrorResponse ToValidationErrorResponse(
        AppRoute.RouteValidationError error) =>
        new(error.Field, error.Code, error.Message);

    /// <summary>
    /// Replaces a route's configuration.
    /// </summary>
    /// <remarks>
    /// The stored route ends up holding exactly what the body says, so an option
    /// block that is no longer present is removed and a null host is cleared.
    /// A body that cannot be mapped is rejected against the field at fault.
    /// </remarks>
    [HttpPut("{id}")]
    public async Task<ActionResult<ApiDtos.RouteResponse>> UpdateRoute(
        string id,
        ApiDtos.UpdateRouteRequest request)
    {
        try
        {
            var routeId = RouteId.From(id);

            var mapping = RouteRequestMapper.ToReplaceCommand(
                request,
                routeId,
                User.Identity?.Name ?? "system");

            if (!mapping.Success)
            {
                return BadRequest(FieldErrors(mapping.Errors));
            }

            var result = await _replaceRouteCommandHandler.HandleAsync(mapping.Value!);
            if (result == null)
                return NotFound();

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
                RouteId.From(id),
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
                RouteId.From(id),
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
                RouteId.From(id),
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
                RouteId.From(id),
                User.Identity?.Name ?? "system"
            );

            var result = await _validateRouteCommandHandler.HandleAsync(command);
            return HandleResult(ToValidationResponse(result));
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
            var query = new AppRoute.PreviewRouteQuery(RouteId.From(id));
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
            var query = new AppRoute.GetEffectiveRouteQuery(RouteId.From(id));
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
            var query = new AppRoute.RouteHistoryQuery(RouteId.From(id));
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

    /// <summary>
    /// Projects the application response onto the wire shape.
    /// </summary>
    /// <remarks>
    /// The option blocks are passed through as the domain holds them. Rates and
    /// the like are reported as configured simply by being present, which is why
    /// EnableRateLimiting is derived here rather than stored.
    /// </remarks>
    private static ApiDtos.RouteResponse MapToResponse(AppRoute.RouteResponse route) =>
        new(
            route.Id.Value.ToString(),
            route.Key,
            route.Method.Value,
            route.UpstreamPath.Value,
            null,
            route.ServiceId.Value.ToString(),
            route.IsEnabled,
            route.DownstreamTargets
                .Select(t => new ApiDtos.DownstreamTargetResponse(t.Host, t.Port, t.Scheme, t.Path))
                .ToList(),
            route.AuthenticationOptions != null
                ? new ApiDtos.AuthenticationOptionsResponse(
                    route.AuthenticationOptions.Properties?.TryGetValue("scopes", out var scopes) == true
                        ? scopes.Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .Select(scope => scope.Trim())
                            .Where(scope => scope.Length > 0)
                            .ToList()
                        : new List<string>())
                : null,
            route.AuthorizationOptions != null
                ? new ApiDtos.AuthorizationOptionsResponse(
                    route.AuthorizationOptions.Policies,
                    route.AuthorizationOptions.Scopes,
                    route.AuthorizationOptions.Requirements)
                : null,
            route.RateLimitOptions != null
                ? new ApiDtos.RateLimitOptionsResponse(
                    true,
                    route.RateLimitOptions.Period ?? "Second",
                    route.RateLimitOptions.Limit ?? 0)
                : null,
            route.QoSOptions != null
                ? new ApiDtos.QoSOptionsResponse(
                    route.QoSOptions.TimeoutSeconds ?? 0,
                    route.QoSOptions.CircuitBreakerTimeoutSeconds)
                : null,
            route.CacheOptions != null
                ? new ApiDtos.CacheOptionsResponse(route.CacheOptions.TtlSeconds)
                : null,
            route.LoadBalancerOptions != null
                ? new ApiDtos.LoadBalancerOptionsResponse(route.LoadBalancerOptions.Algorithm)
                : null,
            ToTransformationsResponse(route.HeaderOptions),
            ToTransformationsResponse(route.ClaimOptions),
            ToTransformationsResponse(route.QueryOptions),
            route.Priority,
            route.RouteIsCaseSensitive,
            route.DownstreamMethod?.Value,
            route.DownstreamTemplate?.Value,
            route.DownstreamHttpVersion,
            route.DownstreamHttpVersionPolicy,
            route.DangerousAcceptAnyServerCertificateValidator,
            route.DelegatingHandlers.ToList(),
            route.HttpClientOptions == null
                ? null
                : new ApiDtos.HttpClientOptionsResponse(
                    route.HttpClientOptions.AllowAutoRedirect,
                    route.HttpClientOptions.MaxConnectionsPerServer,
                    route.HttpClientOptions.PooledConnectionLifetimeSeconds,
                    route.HttpClientOptions.UseCookieContainer,
                    route.HttpClientOptions.UseProxy,
                    route.HttpClientOptions.UseTracing),
            route.TimeoutSeconds,
            route.CreatedAt,
            route.UpdatedAt);

    private static ApiDtos.TransformationsResponse? ToTransformationsResponse<TTransform>(
        IReadOnlyList<TTransform>? add,
        IReadOnlyList<string>? remove,
        IReadOnlyList<TTransform>? transform,
        Func<TTransform, string> key,
        Func<TTransform, string> value)
        where TTransform : class =>
        add == null && remove == null && transform == null
            ? null
            : new ApiDtos.TransformationsResponse(
                add?.Select(t => new ApiDtos.TransformEntryResponse(key(t), value(t))).ToList()
                    ?? new List<ApiDtos.TransformEntryResponse>(),
                remove?.ToList() ?? new List<string>(),
                transform?.Select(t => new ApiDtos.TransformEntryResponse(key(t), value(t))).ToList()
                    ?? new List<ApiDtos.TransformEntryResponse>());

    private static ApiDtos.TransformationsResponse? ToTransformationsResponse(
        DomainHeaderOptions? options) =>
        options == null
            ? null
            : ToTransformationsResponse(
                options.Add,
                options.Remove,
                options.Transform,
                t => t.Key,
                t => t.Value);

    private static ApiDtos.TransformationsResponse? ToTransformationsResponse(
        DomainClaimOptions? options) =>
        options == null
            ? null
            : ToTransformationsResponse(
                options.Add,
                options.Remove,
                options.Transform,
                t => t.Key,
                t => t.Value);

    private static ApiDtos.TransformationsResponse? ToTransformationsResponse(
        DomainQueryOptions? options) =>
        options == null
            ? null
            : ToTransformationsResponse(
                options.Add,
                options.Remove,
                options.Transform,
                t => t.Key,
                t => t.Value);

    private static ApiDtos.RouteHistoryItem MapToHistoryItem(AppRoute.RouteHistoryItem item)
    {
        return new ApiDtos.RouteHistoryItem(item.Timestamp, item.Action, item.ChangedBy, item.Details);
    }
}
