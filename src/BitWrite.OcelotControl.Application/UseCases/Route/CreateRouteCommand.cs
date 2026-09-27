using DomainHttpMethod = BitWrite.OcelotControl.Domain.ValueObjects.Configuration.HttpMethod;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Domain.ValueObjects.FeatureConfig;

namespace BitWrite.OcelotControl.Application.UseCases.Route;

public record CreateRouteCommand(
    string Key,
    DomainHttpMethod Method,
    UpstreamPath UpstreamPath,
    ServiceId ServiceId,
    IReadOnlyList<DownstreamTarget> DownstreamTargets,
    string? Host = null,
    AuthenticationOptions? AuthenticationOptions = null,
    AuthorizationOptions? AuthorizationOptions = null,
    RateLimitOptions? RateLimitOptions = null,
    QoSOptions? QoSOptions = null,
    CacheOptions? CacheOptions = null,
    LoadBalancerOptions? LoadBalancerOptions = null,
    HeaderOptions? HeaderOptions = null,
    ClaimOptions? ClaimOptions = null,
    QueryOptions? QueryOptions = null,
    string InitiatedBy = "",
    string CorrelationId = ""
);