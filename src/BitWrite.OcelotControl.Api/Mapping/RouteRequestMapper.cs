using BitWrite.OcelotControl.Application.UseCases.Route;
using ApiDtos = BitWrite.OcelotControl.Api.DTOs;
using BitWrite.OcelotControl.Domain.Services;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.FeatureConfig;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using DomainHttpMethod = BitWrite.OcelotControl.Domain.ValueObjects.Configuration.HttpMethod;

namespace BitWrite.OcelotControl.Api.Mapping;

/// <summary>
/// The outcome of mapping a route request body onto domain value objects.
/// </summary>
public sealed record RouteMappingResult<T>(
    bool Success,
    T? Value,
    List<RouteValidationError> Errors)
{
    public static RouteMappingResult<T> Failed(List<RouteValidationError> errors) =>
        new(false, default, errors);

    public static RouteMappingResult<T> Ok(T value) => new(true, value, new List<RouteValidationError>());
}

/// <summary>
/// Maps a route request body onto domain value objects.
/// </summary>
/// <remarks>
/// Shared by create and draft validation so the two cannot disagree about what
/// a valid request is — a draft that validates here is a request that will
/// create cleanly.
/// <para>
/// The value objects reject bad input by throwing, which is right for the
/// domain but wrong for a request. Every failure is captured against the field
/// it came from instead, so the wizard can send the operator back to the step
/// that can fix it.
/// </para>
/// </remarks>
public static class RouteRequestMapper
{
    public static RouteMappingResult<CreateRouteCommand> ToCreateCommand(
        ApiDtos.CreateRouteRequest request,
        string initiatedBy)
    {
        var mapping = MapCommon(
            request.Method,
            request.UpstreamPath,
            request.ServiceId,
            request.DownstreamTargets,
            request.Host);
        if (!mapping.Success) return RouteMappingResult<CreateRouteCommand>.Failed(mapping.Errors);

        var authentication = BuildAuthentication(request.AuthenticationOptions, mapping.Errors);
        var rateLimit = BuildRateLimit(request.RateLimitOptions, mapping.Errors);
        var qos = BuildQoS(request.QoSOptions, mapping.Errors);
        var cache = BuildCache(request.CacheOptions, mapping.Errors);
        var loadBalancer = BuildLoadBalancer(request.LoadBalancerOptions, mapping.Errors);

        if (mapping.Errors.Count > 0)
        {
            return RouteMappingResult<CreateRouteCommand>.Failed(mapping.Errors);
        }

        return RouteMappingResult<CreateRouteCommand>.Ok(new CreateRouteCommand(
            request.Key,
            mapping.Value!.Method,
            mapping.Value.UpstreamPath,
            mapping.Value.ServiceId,
            mapping.Value.DownstreamTargets,
            request.Host,
            authentication,
            rateLimit,
            qos,
            cache,
            loadBalancer,
            initiatedBy));
    }

    /// <summary>
    /// Maps a replacement body onto a command that replaces rather than merges.
    /// </summary>
    /// <remarks>
    /// Reuses the create mapping so the two endpoints cannot disagree about what
    /// a valid route is. Optional blocks come through as null, which the replace
    /// path treats as "remove this".
    /// </remarks>
    public static RouteMappingResult<ReplaceRouteCommand> ToReplaceCommand(
        ApiDtos.UpdateRouteRequest request,
        RouteId id,
        string initiatedBy)
    {
        var mapping = MapCommon(
            request.Method,
            request.UpstreamPath,
            request.ServiceId,
            request.DownstreamTargets,
            request.Host);

        if (!mapping.Success) return RouteMappingResult<ReplaceRouteCommand>.Failed(mapping.Errors);

        var authentication = BuildAuthentication(request.AuthenticationOptions, mapping.Errors);
        var rateLimit = BuildRateLimit(request.RateLimitOptions, mapping.Errors);
        var qos = BuildQoS(request.QoSOptions, mapping.Errors);
        var cache = BuildCache(request.CacheOptions, mapping.Errors);
        var loadBalancer = BuildLoadBalancer(request.LoadBalancerOptions, mapping.Errors);

        if (mapping.Errors.Count > 0)
        {
            return RouteMappingResult<ReplaceRouteCommand>.Failed(mapping.Errors);
        }

        return RouteMappingResult<ReplaceRouteCommand>.Ok(new ReplaceRouteCommand(
            id,
            mapping.Value!.Method,
            mapping.Value.UpstreamPath,
            mapping.Value.ServiceId,
            mapping.Value.DownstreamTargets,
            request.Key,
            request.Host,
            authentication,
            rateLimit,
            qos,
            cache,
            loadBalancer,
            initiatedBy));
    }

    /// <summary>
    /// Maps a request body onto the shared validator's input, so an unsaved
    /// draft runs the same checks a stored route does.
    /// </summary>
    public static RouteMappingResult<RouteValidationInput> ToValidationInput(
        ApiDtos.CreateRouteRequest request)
    {
        var mapping = MapCommon(
            request.Method,
            request.UpstreamPath,
            request.ServiceId,
            request.DownstreamTargets,
            request.Host);
        if (!mapping.Success) return RouteMappingResult<RouteValidationInput>.Failed(mapping.Errors);

        var features = new List<string>();
        var authentication = BuildAuthentication(request.AuthenticationOptions, mapping.Errors);
        var rateLimit = BuildRateLimit(request.RateLimitOptions, mapping.Errors);
        var qos = BuildQoS(request.QoSOptions, mapping.Errors);
        var cache = BuildCache(request.CacheOptions, mapping.Errors);
        var loadBalancer = BuildLoadBalancer(request.LoadBalancerOptions, mapping.Errors);

        if (authentication is not null) features.Add("authentication");
        if (rateLimit is not null) features.Add("rate-limiting");
        if (qos is not null) features.Add("qos");
        if (cache is not null) features.Add("caching");
        if (loadBalancer is not null) features.Add("load-balancing");

        if (mapping.Errors.Count > 0)
        {
            return RouteMappingResult<RouteValidationInput>.Failed(mapping.Errors);
        }

        var configuration = new RouteConfiguration
        {
            // A draft is not persisted; every downstream check reads the
            // configuration rather than the id.
            Id = default!,
            Host = request.Host,
            Method = mapping.Value!.Method,
            UpstreamPath = mapping.Value.UpstreamPath,
            ServiceId = mapping.Value.ServiceId,
            DownstreamTargets = mapping.Value.DownstreamTargets,
            AuthenticationOptions = authentication,
            RateLimitOptions = rateLimit,
            QoSOptions = qos,
            CacheOptions = cache,
            LoadBalancerOptions = loadBalancer
        };

        return RouteMappingResult<RouteValidationInput>.Ok(new RouteValidationInput(
            configuration,
            mapping.Value!.Key,
            // Nothing to exclude: a draft is not in the repository yet.
            ExistingRouteId: null,
            Features: features));
    }

    /// <summary>
    /// The fields both entry points need, mapped once.
    /// </summary>
    private static RouteMappingResult<CommonFields> MapCommon(
        string method,
        string upstreamPathValue,
        string serviceIdValue,
        IReadOnlyList<ApiDtos.DownstreamTargetRequest>? requestTargets,
        string? host)
    {
        var errors = new List<RouteValidationError>();
        var targets = new List<DownstreamTarget>();

        DomainHttpMethod? parsedMethod = null;
        try
        {
            parsedMethod = DomainHttpMethod.Parse(method);
        }
        catch (Exception ex)
        {
            errors.Add(new RouteValidationError("method", "INVALID_METHOD", ex.Message));
        }

        UpstreamPath? upstreamPath = null;
        try
        {
            upstreamPath = UpstreamPath.From(upstreamPathValue);
        }
        catch (Exception ex)
        {
            errors.Add(new RouteValidationError("upstreamPath", "INVALID_UPSTREAM_PATH", ex.Message));
        }

        ServiceId? serviceId = null;
        try
        {
            serviceId = ServiceId.From(serviceIdValue);
        }
        catch (Exception ex)
        {
            errors.Add(new RouteValidationError("serviceId", "INVALID_SERVICE_ID", ex.Message));
        }

        for (var index = 0; index < (requestTargets?.Count ?? 0); index++)
        {
            var target = requestTargets![index];
            try
            {
                targets.Add(DownstreamTarget.Create(target.Scheme, target.Host, target.Port, target.Path));
            }
            catch (Exception ex)
            {
                // Indexed, so the wizard can point at the offending target row.
                errors.Add(new RouteValidationError(
                    $"downstreamTargets[{index}]",
                    "INVALID_TARGET",
                    ex.Message));
            }
        }

        // Conflicts are detected on the derived signature, not the friendly
        // key: this is the same value Route.RouteKey exposes, so a draft
        // conflicts with exactly the routes a saved one would.
        var key = parsedMethod is not null && upstreamPath is not null
            ? RouteKey.Create(parsedMethod, upstreamPath, host)
            : null;

        if (key is null)
        {
            errors.Add(new RouteValidationError(
                "upstreamPath",
                "INCOMPLETE_ROUTE",
                "A method and an upstream path are required to identify the route"));
        }

        var fields = new CommonFields(parsedMethod!, upstreamPath!, serviceId!, key!, targets);

        return errors.Count > 0
            ? RouteMappingResult<CommonFields>.Failed(errors)
            : RouteMappingResult<CommonFields>.Ok(fields);
    }

    private static AuthenticationOptions? BuildAuthentication(
        ApiDtos.AuthenticationOptionsRequest? request,
        List<RouteValidationError> errors)
    {
        if (request is null) return null;

        try
        {
            return AuthenticationOptions.Create(
                "Bearer",
                null,
                new Dictionary<string, string>
                {
                    ["scopes"] = string.Join(
                        ",",
                        request.AllowedScopes ?? new List<string>())
                });
        }
        catch (Exception ex)
        {
            errors.Add(new RouteValidationError(
                "authenticationOptions.allowedScopes",
                "INVALID_AUTHENTICATION",
                ex.Message));
            return null;
        }
    }

    private static RateLimitOptions? BuildRateLimit(
        ApiDtos.RateLimitOptionsRequest? request,
        List<RouteValidationError> errors)
    {
        if (request is null) return null;

        try
        {
            return RateLimitOptions.Create(request.Limit, request.Period);
        }
        catch (Exception ex)
        {
            errors.Add(new RouteValidationError("rateLimitOptions", "INVALID_RATE_LIMIT", ex.Message));
            return null;
        }
    }

    private static QoSOptions? BuildQoS(
        ApiDtos.QoSOptionsRequest? request,
        List<RouteValidationError> errors)
    {
        if (request is null) return null;

        try
        {
            return QoSOptions.Create(
                request.TimeoutSeconds,
                circuitBreakerTimeoutSeconds: request.CircuitBreakerTimeoutSeconds);
        }
        catch (Exception ex)
        {
            errors.Add(new RouteValidationError("qosOptions", "INVALID_QOS", ex.Message));
            return null;
        }
    }

    private static CacheOptions? BuildCache(
        ApiDtos.CacheOptionsRequest? request,
        List<RouteValidationError> errors)
    {
        if (request is null) return null;

        try
        {
            return CacheOptions.Create(request.TtlSeconds);
        }
        catch (Exception ex)
        {
            errors.Add(new RouteValidationError("cacheOptions", "INVALID_CACHE", ex.Message));
            return null;
        }
    }

    private static LoadBalancerOptions? BuildLoadBalancer(
        ApiDtos.LoadBalancerOptionsRequest? request,
        List<RouteValidationError> errors)
    {
        if (request is null) return null;

        try
        {
            return LoadBalancerOptions.Create(request.Algorithm);
        }
        catch (Exception ex)
        {
            errors.Add(new RouteValidationError("loadBalancerOptions", "INVALID_LOAD_BALANCER", ex.Message));
            return null;
        }
    }

    private sealed record CommonFields(
        DomainHttpMethod Method,
        UpstreamPath UpstreamPath,
        ServiceId ServiceId,
        RouteKey Key,
        IReadOnlyList<DownstreamTarget> DownstreamTargets);
}
