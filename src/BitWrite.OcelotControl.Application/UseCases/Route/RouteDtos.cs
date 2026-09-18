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