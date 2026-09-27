using System.ComponentModel.DataAnnotations;

namespace BitWrite.OcelotControl.Api.DTOs;

/// <summary>
/// Authorization rules for a route.
/// </summary>
/// <remarks>
/// Separate from authentication, which says who the caller is. A caller can be
/// authenticated and still not be allowed on this route, so the two are not
/// conflated: authenticationOptions.allowedScopes keeps its existing meaning.
/// </remarks>
public record AuthorizationOptionsRequest(
    List<string>? Policies,
    List<string>? Scopes,
    Dictionary<string, string>? Requirements
);

/// <summary>
/// One key/value transformation, used for headers, claims and query strings.
/// </summary>
public record TransformEntryRequest(
    [Required][MaxLength(200)] string Key,
    [MaxLength(2000)] string Value
);

/// <summary>
/// Transformations to add, to remove, and to rewrite in place.
/// </summary>
public record TransformationsRequest(
    List<TransformEntryRequest>? Add,
    List<string>? Remove,
    List<TransformEntryRequest>? Transform
);

public record CreateRouteRequest(
    [Required][MaxLength(100)] string Key,
    [Required] string Method,
    [Required][MaxLength(500)] string UpstreamPath,
    [MaxLength(100)] string? Host,
    [Required] string ServiceId,
    [Required] List<DownstreamTargetRequest> DownstreamTargets,
    AuthenticationOptionsRequest? AuthenticationOptions,
    AuthorizationOptionsRequest? AuthorizationOptions,
    RateLimitOptionsRequest? RateLimitOptions,
    QoSOptionsRequest? QoSOptions,
    CacheOptionsRequest? CacheOptions,
    LoadBalancerOptionsRequest? LoadBalancerOptions,
    TransformationsRequest? HeaderTransformations,
    TransformationsRequest? ClaimTransformations,
    TransformationsRequest? QueryTransformations
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
    AuthorizationOptionsRequest? AuthorizationOptions,
    RateLimitOptionsRequest? RateLimitOptions,
    QoSOptionsRequest? QoSOptions,
    CacheOptionsRequest? CacheOptions,
    LoadBalancerOptionsRequest? LoadBalancerOptions,
    TransformationsRequest? HeaderTransformations,
    TransformationsRequest? ClaimTransformations,
    TransformationsRequest? QueryTransformations
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
    AuthorizationOptionsResponse? AuthorizationOptions,
    RateLimitOptionsResponse? RateLimitOptions,
    QoSOptionsResponse? QoSOptions,
    CacheOptionsResponse? CacheOptions,
    LoadBalancerOptionsResponse? LoadBalancerOptions,
    TransformationsResponse? HeaderTransformations,
    TransformationsResponse? ClaimTransformations,
    TransformationsResponse? QueryTransformations,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

/// <summary>Authorization rules as stored on a route.</summary>
public record AuthorizationOptionsResponse(
    List<string> Policies,
    List<string> Scopes,
    Dictionary<string, string> Requirements
);

/// <summary>One stored key/value transformation.</summary>
public record TransformEntryResponse(string Key, string Value);

/// <summary>Transformations as stored on a route.</summary>
public record TransformationsResponse(
    List<TransformEntryResponse> Add,
    List<string> Remove,
    List<TransformEntryResponse> Transform
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