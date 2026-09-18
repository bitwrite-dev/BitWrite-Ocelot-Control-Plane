using BitWrite.OcelotControl.Domain.Aggregates.GlobalConfiguration;

namespace BitWrite.OcelotControl.Application.UseCases.GlobalConfiguration;

public record UpdateGlobalConfigurationCommand(
    string? BaseUrl = null,
    string? RequestIdKey = null,
    string? DownstreamScheme = null,
    int? Timeout = null,
    RateLimitConfig? RateLimit = null,
    QoSConfig? QoS = null,
    HttpHandlerConfig? HttpHandler = null,
    ServiceDiscoveryConfig? ServiceDiscovery = null,
    string InitiatedBy = "",
    string CorrelationId = ""
);