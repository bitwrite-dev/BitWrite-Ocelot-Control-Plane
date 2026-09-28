using DomainRoute = BitWrite.OcelotControl.Domain.Aggregates.Route.Route;
using BitWrite.OcelotControl.Domain.Services;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;

namespace BitWrite.OcelotControl.Application.UseCases.Route;

/// <summary>
/// Projects a route aggregate onto the builder's input shape.
/// </summary>
/// <remarks>
/// Four copies of this mapping existed — preview, effective, validation and the
/// snapshot path. They were identical, which is exactly the problem: a new field
/// meant four edits, and a copy that was missed dropped the field from the
/// generated configuration. In the snapshot copy that fails silently, since the
/// published configuration is what the gateway actually reads.
/// </remarks>
public static class RouteConfigurationMapper
{
    public static RouteConfiguration Map(DomainRoute route) =>
        new()
        {
            Id = route.Id,
            Host = route.Host,
            FriendlyKey = route.Key,
            Method = route.Method,
            UpstreamPath = route.UpstreamPath,
            ServiceId = route.ServiceId,
            DownstreamTargets = route.DownstreamTargets
                .Select(t => DownstreamTarget.Create(t.Scheme, t.Host, t.Port, t.Path))
                .ToList(),
            AuthenticationOptions = route.AuthenticationOptions,
            AuthorizationOptions = route.AuthorizationOptions,
            RateLimitOptions = route.RateLimitOptions,
            QoSOptions = route.QoSOptions,
            CacheOptions = route.CacheOptions,
            LoadBalancerOptions = route.LoadBalancerOptions,
            HeaderOptions = route.HeaderOptions,
            ClaimOptions = route.ClaimOptions,
            QueryOptions = route.QueryOptions,
            Priority = route.Priority,
            RouteIsCaseSensitive = route.RouteIsCaseSensitive,
            DownstreamTemplate = route.DownstreamTemplate,
            DownstreamMethod = route.DownstreamMethod,
            DownstreamHttpVersion = route.DownstreamHttpVersion,
            DownstreamHttpVersionPolicy = route.DownstreamHttpVersionPolicy,
            DangerousAcceptAnyServerCertificateValidator =
                route.DangerousAcceptAnyServerCertificateValidator,
            DelegatingHandlers = route.DelegatingHandlers.ToList(),
            HttpClientOptions = route.HttpClientOptions,
            TimeoutSeconds = route.TimeoutSeconds
        };
}
