using DomainHttpMethod = BitWrite.OcelotControl.Domain.ValueObjects.Configuration.HttpMethod;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.FeatureConfig;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;

namespace BitWrite.OcelotControl.Application.UseCases.Route;

public record RouteResponse(
    RouteId Id,
    string Key,
    DomainHttpMethod Method,
    UpstreamPath UpstreamPath,
    ServiceId ServiceId,
    bool IsEnabled,
    IReadOnlyList<DownstreamTarget> DownstreamTargets,
    AuthenticationOptions? AuthenticationOptions,
    AuthorizationOptions? AuthorizationOptions,
    RateLimitOptions? RateLimitOptions,
    QoSOptions? QoSOptions,
    CacheOptions? CacheOptions,
    LoadBalancerOptions? LoadBalancerOptions,
    HeaderOptions? HeaderOptions,
    ClaimOptions? ClaimOptions,
    QueryOptions? QueryOptions,
    int Priority,
    bool RouteIsCaseSensitive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

public record RouteListResponse(
    IReadOnlyList<RouteResponse> Routes,
    int TotalCount,
    int Page,
    int PageSize
);

public record ListRoutesQuery(
    int Page = 1,
    int PageSize = 20,
    string? ServiceId = null,
    bool? IsEnabled = null,
    string? Search = null
);

public record GetRouteQuery(
    RouteId Id
);

public record RouteHistoryQuery(
    RouteId Id
);

public record RouteHistoryResponse(
    IReadOnlyList<RouteHistoryItem> History
);

public record RouteHistoryItem(
    DateTimeOffset Timestamp,
    string Action,
    string? ChangedBy,
    string? Details
);

public record ValidateRouteCommand(
    RouteId Id,
    string InitiatedBy = "",
    string CorrelationId = ""
);

/// <summary>
/// Validates a route the operator has not saved yet.
/// </summary>
/// <remarks>
/// The body is mapped by the API layer, so the handler only runs the shared
/// checks. Kept beside ValidateRouteCommand so both entry points are discoverable.
/// </remarks>
public record ValidateRouteDraftCommand(
    RouteValidationInput Input,
    string InitiatedBy = "",
    string CorrelationId = ""
);

public record PreviewRouteQuery(
    RouteId Id
);

public record PreviewRouteResponse(
    string OcelotJson
);

public record GetEffectiveRouteQuery(
    RouteId Id
);

public record GetEffectiveRouteResponse(
    string OcelotJson
);