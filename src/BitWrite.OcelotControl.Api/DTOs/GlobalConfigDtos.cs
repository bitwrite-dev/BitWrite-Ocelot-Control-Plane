using System.ComponentModel.DataAnnotations;

namespace BitWrite.OcelotControl.Api.DTOs;

public record UpdateGlobalConfigurationRequest(
    [MaxLength(200)] string? BaseUrl,
    [MaxLength(100)] string? RequestIdKey,
    [MaxLength(50)] string? DownstreamScheme,
    [Range(1, 300000)] int? Timeout,
    RateLimitConfigRequest? RateLimit,
    QoSConfigRequest? QoS,
    HttpHandlerConfigRequest? HttpHandler,
    ServiceDiscoveryConfigRequest? ServiceDiscovery
);

public record RateLimitConfigRequest(
    bool EnableRateLimiting,
    [MaxLength(10)] string? HttpStatusCode
);

public record QoSConfigRequest(
    [Range(1, 3600)] int TimeoutValue,
    [Range(1, 3600)] int DurationOfBreak
);

public record HttpHandlerConfigRequest(
    bool UseProxy,
    bool Expect100Continue,
    [Range(1, 10000)] int? MaxConnectionsPerServer
);

public record ServiceDiscoveryConfigRequest(
    [MaxLength(100)] string? Provider,
    [MaxLength(200)] string? Host,
    [Range(1, 65535)] int? Port,
    [MaxLength(100)] string? Type,
    Dictionary<string, string>? Configuration
);

public record GlobalConfigurationResponse(
    string Id,
    string? BaseUrl,
    string? RequestIdKey,
    string? DownstreamScheme,
    int? Timeout,
    RateLimitConfigResponse? RateLimit,
    QoSConfigResponse? QoS,
    HttpHandlerConfigResponse? HttpHandler,
    ServiceDiscoveryConfigResponse? ServiceDiscovery,
    DateTimeOffset UpdatedAt
);

public record RateLimitConfigResponse(
    bool EnableRateLimiting,
    string? HttpStatusCode
);

public record QoSConfigResponse(
    int TimeoutValue,
    int DurationOfBreak
);

public record HttpHandlerConfigResponse(
    bool UseProxy,
    bool Expect100Continue,
    int? MaxConnectionsPerServer
);

public record ServiceDiscoveryConfigResponse(
    string? Provider,
    string? Host,
    int? Port,
    string? Type,
    Dictionary<string, string> Configuration
);