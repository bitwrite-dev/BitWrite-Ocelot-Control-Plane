using System.ComponentModel.DataAnnotations;

namespace BitWrite.OcelotControl.SDK.Models.Requests;

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