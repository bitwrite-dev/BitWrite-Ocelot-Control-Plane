using System.ComponentModel.DataAnnotations;

namespace BitWrite.OcelotControl.Api.DTOs;

public record CreateServiceRequest(
    [Required][MaxLength(200)] string Name,
    [MaxLength(1000)] string? Description,
    List<DownstreamTargetRequest>? DownstreamTargets
);

public record UpdateServiceRequest(
    [MaxLength(200)] string? Name,
    [MaxLength(1000)] string? Description,
    List<DownstreamTargetRequest>? DownstreamTargets
);

public record ServiceResponse(
    string Id,
    string Name,
    string? Description,
    List<DownstreamTargetResponse> DownstreamTargets,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

public record ServiceListResponse(
    List<ServiceResponse> Services,
    int TotalCount,
    int Page,
    int PageSize
);

public record ServiceRoutesResponse(
    List<RouteResponse> Routes
);