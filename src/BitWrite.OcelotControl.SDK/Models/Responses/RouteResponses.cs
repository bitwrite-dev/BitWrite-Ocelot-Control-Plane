using System.ComponentModel.DataAnnotations;

namespace BitWrite.OcelotControl.SDK.Models.Responses;

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