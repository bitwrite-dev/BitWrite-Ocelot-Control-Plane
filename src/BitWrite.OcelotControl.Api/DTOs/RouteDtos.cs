using System.ComponentModel.DataAnnotations;

namespace BitWrite.OcelotControl.Api.DTOs;

public record CreateRouteRequest(
    [Required][MaxLength(100)] string Key,
    [Required] string Method,
    [Required][MaxLength(500)] string UpstreamPath,
    [MaxLength(100)] string? Host,
    [Required] string ServiceId,
    [Required] List<DownstreamTargetRequest> DownstreamTargets,
    AuthenticationOptionsRequest? AuthenticationOptions,
    RateLimitOptionsRequest? RateLimitOptions,
    QoSOptionsRequest? QoSOptions,
    CacheOptionsRequest? CacheOptions,
    LoadBalancerOptionsRequest? LoadBalancerOptions
);

public record UpdateRouteRequest(
    [MaxLength(100)] string? Key,
    string? Method,
    [MaxLength(500)] string? UpstreamPath,
    [MaxLength(100)] string? Host,
    string? ServiceId,
    List<DownstreamTargetRequest>? DownstreamTargets,
    AuthenticationOptionsRequest? AuthenticationOptions,
    RateLimitOptionsRequest? RateLimitOptions,
    QoSOptionsRequest? QoSOptions,
    CacheOptionsRequest? CacheOptions,
    LoadBalancerOptionsRequest? LoadBalancerOptions
);

public record DownstreamTargetRequest(
    [Required][MaxLength(200)] string Host,
    [Required][Range(1, 65535)] int Port,
    string Scheme = "http",
    string Path = "/"
);

public record AuthenticationOptionsRequest(
    List<string>? AllowedScopes
);

public record RateLimitOptionsRequest(
    [Required] bool EnableRateLimiting,
    [Required][MaxLength(50)] string Period,
    [Required][Range(1, int.MaxValue)] int Limit
);

public record QoSOptionsRequest(
    [Required][Range(1, 3600)] int TimeoutSeconds,
    [Range(1, 3600)] int? CircuitBreakerTimeoutSeconds
);

public record CacheOptionsRequest(
    [Required][Range(1, 86400)] int TtlSeconds
);

public record LoadBalancerOptionsRequest(
    [Required][MaxLength(50)] string Algorithm
);

public record RouteResponse(
    string Id,
    string Key,
    string Method,
    string UpstreamPath,
    string? Host,
    string ServiceId,
    bool IsEnabled,
    List<DownstreamTargetResponse> DownstreamTargets,
    AuthenticationOptionsResponse? AuthenticationOptions,
    RateLimitOptionsResponse? RateLimitOptions,
    QoSOptionsResponse? QoSOptions,
    CacheOptionsResponse? CacheOptions,
    LoadBalancerOptionsResponse? LoadBalancerOptions,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

public record DownstreamTargetResponse(
    string Host,
    int Port,
    string Scheme,
    string Path
);

public record AuthenticationOptionsResponse(
    List<string> AllowedScopes
);

public record RateLimitOptionsResponse(
    bool EnableRateLimiting,
    string Period,
    int Limit
);

public record QoSOptionsResponse(
    int TimeoutSeconds,
    int? CircuitBreakerTimeoutSeconds
);

public record CacheOptionsResponse(
    int TtlSeconds
);

public record LoadBalancerOptionsResponse(
    string Algorithm
);

public record RouteListResponse(
    List<RouteResponse> Routes,
    int TotalCount,
    int Page,
    int PageSize
);

public record RouteValidationResponse(
    bool IsValid,
    List<string> Errors
);

public record RoutePreviewResponse(
    string OcelotJson
);

public record RouteEffectiveResponse(
    string OcelotJson
);

public record RouteHistoryResponse(
    List<RouteHistoryItem> History
);

public record RouteHistoryItem(
    DateTimeOffset Timestamp,
    string Action,
    string? ChangedBy,
    string? Details
);