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

/// <summary>
/// Replaces a route's configuration.
/// </summary>
/// <remarks>
/// A replacement, not a merge: the result holds exactly what this body says.
/// The identifying fields are therefore required, while everything optional is
/// nullable and null means "remove it". A null for a required field is rejected
/// rather than treated as a request to blank the route.
/// </remarks>
public record UpdateRouteRequest(
    [MaxLength(100)] string? Key,
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

/// <summary>
/// A validation failure, tagged with the request field that caused it.
/// </summary>
/// <param name="Field">
/// Null when the problem belongs to the route as a whole rather than one field.
/// </param>
public record RouteValidationErrorResponse(
    string? Field,
    string Code,
    string Message
);

/// <summary>
/// Field-level validation errors, so a wizard can send the operator back to the
/// step that owns the offending field instead of showing one flat list.
/// </summary>
public record RouteValidationResponse(
    bool IsValid,
    List<RouteValidationErrorResponse> Errors
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