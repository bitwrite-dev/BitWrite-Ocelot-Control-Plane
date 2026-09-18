using BitWrite.OcelotControl.Domain.Aggregates.GlobalConfiguration;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.FeatureConfig;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;

namespace BitWrite.OcelotControl.Application.UseCases.GlobalConfiguration;

public record GlobalConfigurationResponse(
    string Id,
    string? BaseUrl,
    string? RequestIdKey,
    string? DownstreamScheme,
    int? Timeout,
    RateLimitConfig? RateLimit,
    QoSConfig? QoS,
    HttpHandlerConfig? HttpHandler,
    ServiceDiscoveryConfig? ServiceDiscovery,
    DateTimeOffset UpdatedAt
);