using DomainHttpMethod = BitWrite.OcelotControl.Domain.ValueObjects.Configuration.HttpMethod;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Domain.ValueObjects.FeatureConfig;

namespace BitWrite.OcelotControl.Application.UseCases.Route;

public record UpdateRouteCommand(
    RouteId Id,
    string? Key = null,
    DomainHttpMethod? Method = null,
    UpstreamPath? UpstreamPath = null,
    ServiceId? ServiceId = null,
    IReadOnlyList<DownstreamTarget>? DownstreamTargets = null,
    string? Host = null,
    AuthenticationOptions? AuthenticationOptions = null,
    RateLimitOptions? RateLimitOptions = null,
    QoSOptions? QoSOptions = null,
    CacheOptions? CacheOptions = null,
    LoadBalancerOptions? LoadBalancerOptions = null,
    string InitiatedBy = "",
    string CorrelationId = ""
);