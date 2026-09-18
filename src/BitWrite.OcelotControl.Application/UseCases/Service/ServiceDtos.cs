using BitWrite.OcelotControl.Domain.ValueObjects.Identity;

namespace BitWrite.OcelotControl.Application.UseCases.Service;

public record ServiceResponse(
    ServiceId Id,
    string Name,
    string? Description,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

public record ServiceListResponse(
    IReadOnlyList<ServiceResponse> Services,
    int TotalCount,
    int Page,
    int PageSize
);

public record GetServiceQuery(
    ServiceId Id
);

public record ListServicesQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null
);