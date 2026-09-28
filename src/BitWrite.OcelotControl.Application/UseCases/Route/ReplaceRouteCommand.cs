using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.FeatureConfig;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using DomainHttpMethod = BitWrite.OcelotControl.Domain.ValueObjects.Configuration.HttpMethod;

namespace BitWrite.OcelotControl.Application.UseCases.Route;

/// <summary>
/// Replaces the configuration of an existing route.
/// </summary>
/// <remarks>
/// The identifying fields are non-nullable on purpose: a route cannot exist
/// without them, so a request that omits one is rejected rather than treated as
/// "clear it". Everything that is legitimately optional — the host and each
/// feature block — is nullable, and null means "remove it", which is what makes
/// clearing possible at all.
/// </remarks>
public record ReplaceRouteCommand(
    RouteId Id,
    DomainHttpMethod Method,
    UpstreamPath UpstreamPath,
    ServiceId ServiceId,
    IReadOnlyList<DownstreamTarget> DownstreamTargets,
    string? Key = null,
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
    int priority = 0,
    bool routeIsCaseSensitive = false,
    string InitiatedBy = "",
    DownstreamPathTemplate? DownstreamTemplate = null,
    DomainHttpMethod? DownstreamMethod = null,
    string? DownstreamHttpVersion = null,
    string? DownstreamHttpVersionPolicy = null,
    bool AcceptAnyServerCertificate = false,
    IReadOnlyList<string>? DelegatingHandlers = null,
    HttpClientOptions? HttpClientOptions = null,
    int? TimeoutSeconds = null,
    string CorrelationId = ""
);
