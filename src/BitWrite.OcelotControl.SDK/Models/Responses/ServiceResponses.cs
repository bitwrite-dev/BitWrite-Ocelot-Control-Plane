using System.ComponentModel.DataAnnotations;

namespace BitWrite.OcelotControl.SDK.Models.Responses;

public record ServiceResponse(
    string Id,
    string Name,
    string? Description,
    List<ServiceEndpointResponse> Endpoints,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

public record ServiceEndpointResponse(
    string Host,
    int Port,
    int Weight,
    bool IsActive
);

public record ServiceListResponse(
    List<ServiceResponse> Services,
    int TotalCount,
    int Page,
    int PageSize
);

public record ServiceRoutesResponse(
    List<RouteSummaryResponse> Routes
);

public record RouteSummaryResponse(
    string Id,
    string Key,
    string Method,
    string UpstreamPath,
    bool IsEnabled
);