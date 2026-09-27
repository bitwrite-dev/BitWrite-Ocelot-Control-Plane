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
        var mapping = MapCommon(request);
        if (!mapping.Success) return RouteMappingResult<CreateRouteCommand>.Failed(mapping.Errors);

        var authentication = BuildAuthentication(request, mapping.Errors);
        var rateLimit = BuildRateLimit(request, mapping.Errors);
        var qos = BuildQoS(request, mapping.Errors);
        var cache = BuildCache(request, mapping.Errors);
        var loadBalancer = BuildLoadBalancer(request, mapping.Errors);

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
    /// Maps a request body onto the shared validator's input, so an unsaved
    /// draft runs the same checks a stored route does.
    /// </summary>
    public static RouteMappingResult<RouteValidationInput> ToValidationInput(
        ApiDtos.CreateRouteRequest request)
    {
        var mapping = MapCommon(request);
        if (!mapping.Success) return RouteMappingResult<RouteValidationInput>.Failed(mapping.Errors);

        var features = new List<string>();
        var authentication = BuildAuthentication(request, mapping.Errors);
        var rateLimit = BuildRateLimit(request, mapping.Errors);
        var qos = BuildQoS(request, mapping.Errors);
        var cache = BuildCache(request, mapping.Errors);
        var loadBalancer = BuildLoadBalancer(request, mapping.Errors);

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
        ApiDtos.CreateRouteRequest request)
    {
        var errors = new List<RouteValidationError>();
        var targets = new List<DownstreamTarget>();

        DomainHttpMethod? method = null;
        try
        {
            method = DomainHttpMethod.Parse(request.Method);
        }
        catch (Exception ex)
        {
            errors.Add(new RouteValidationError("method", "INVALID_METHOD", ex.Message));
        }

        UpstreamPath? upstreamPath = null;
        try
        {
            upstreamPath = UpstreamPath.From(request.UpstreamPath);
        }
        catch (Exception ex)
        {
            errors.Add(new RouteValidationError("upstreamPath", "INVALID_UPSTREAM_PATH", ex.Message));
        }

        ServiceId? serviceId = null;
        try
        {
            serviceId = ServiceId.From(request.ServiceId);
        }
        catch (Exception ex)
        {
            errors.Add(new RouteValidationError("serviceId", "INVALID_SERVICE_ID", ex.Message));
        }

        for (var index = 0; index < (request.DownstreamTargets?.Count ?? 0); index++)
        {
            var target = request.DownstreamTargets![index];
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
        var key = method is not null && upstreamPath is not null
            ? RouteKey.Create(method, upstreamPath, request.Host)
            : null;

        if (key is null)
        {
            errors.Add(new RouteValidationError(
                "upstreamPath",
                "INCOMPLETE_ROUTE",
                "A method and an upstream path are required to identify the route"));
        }

        var fields = new CommonFields(method!, upstreamPath!, serviceId!, key!, targets);

        return errors.Count > 0
            ? RouteMappingResult<CommonFields>.Failed(errors)
            : RouteMappingResult<CommonFields>.Ok(fields);
    }

    private static AuthenticationOptions? BuildAuthentication(
        ApiDtos.CreateRouteRequest request,
        List<RouteValidationError> errors)
    {
        if (request.AuthenticationOptions is null) return null;

        try
        {
            return AuthenticationOptions.Create(
                "Bearer",
                null,
                new Dictionary<string, string>
                {
                    ["scopes"] = string.Join(
                        ",",
                        request.AuthenticationOptions.AllowedScopes ?? new List<string>())
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
        ApiDtos.CreateRouteRequest request,
        List<RouteValidationError> errors)
    {
        if (request.RateLimitOptions is null) return null;

        try
        {
            return RateLimitOptions.Create(
                request.RateLimitOptions.Limit,
                request.RateLimitOptions.Period);
        }
        catch (Exception ex)
        {
            errors.Add(new RouteValidationError("rateLimitOptions", "INVALID_RATE_LIMIT", ex.Message));
            return null;
        }
    }

    private static QoSOptions? BuildQoS(
        ApiDtos.CreateRouteRequest request,
        List<RouteValidationError> errors)
    {
        if (request.QoSOptions is null) return null;

        try
        {
            return QoSOptions.Create(
                request.QoSOptions.TimeoutSeconds,
                circuitBreakerTimeoutSeconds: request.QoSOptions.CircuitBreakerTimeoutSeconds);
        }
        catch (Exception ex)
        {
            errors.Add(new RouteValidationError("qosOptions", "INVALID_QOS", ex.Message));
            return null;
        }
    }

    private static CacheOptions? BuildCache(
        ApiDtos.CreateRouteRequest request,
        List<RouteValidationError> errors)
    {
        if (request.CacheOptions is null) return null;

        try
        {
            return CacheOptions.Create(request.CacheOptions.TtlSeconds);
        }
        catch (Exception ex)
        {
            errors.Add(new RouteValidationError("cacheOptions", "INVALID_CACHE", ex.Message));
            return null;
        }
    }

    private static LoadBalancerOptions? BuildLoadBalancer(
        ApiDtos.CreateRouteRequest request,
        List<RouteValidationError> errors)
    {
        if (request.LoadBalancerOptions is null) return null;

        try
        {
            return LoadBalancerOptions.Create(request.LoadBalancerOptions.Algorithm);
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
