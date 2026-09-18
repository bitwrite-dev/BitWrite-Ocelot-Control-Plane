using DomainHttpMethod = BitWrite.OcelotControl.Domain.ValueObjects.Configuration.HttpMethod;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.FeatureConfig;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;

namespace BitWrite.OcelotControl.Application.UseCases.Route;

public record RouteResponse(
    RouteId Id,
    string Key,
    DomainHttpMethod Method,
    UpstreamPath UpstreamPath,
    ServiceId ServiceId,
    bool IsEnabled,
    IReadOnlyList<DownstreamTarget> DownstreamTargets,
    AuthenticationOptions? AuthenticationOptions,
    RateLimitOptions? RateLimitOptions,
    QoSOptions? QoSOptions,
    CacheOptions? CacheOptions,
    LoadBalancerOptions? LoadBalancerOptions,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

public record RouteListResponse(
    IReadOnlyList<RouteResponse> Routes,
    int TotalCount,
    int Page,
    int PageSize
);

public record ValidateRouteCommand(
    RouteId Id,
    string InitiatedBy = "",
    string CorrelationId = ""
);

public record ValidateRouteResponse(
    bool IsValid,
    IReadOnlyList<string> Errors
);

public record PreviewRouteQuery(
    RouteId Id
);

public record PreviewRouteResponse(
    string OcelotJson
);

public record GetEffectiveRouteQuery(
    RouteId Id
);

public record GetEffectiveRouteResponse(
    string OcelotJson
);