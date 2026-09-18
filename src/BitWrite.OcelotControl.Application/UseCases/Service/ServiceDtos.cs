using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;

namespace BitWrite.OcelotControl.Application.UseCases.Service;

public record ServiceResponse(
    ServiceId Id,
    string Name,
    string? Description,
    IReadOnlyList<ServiceEndpoint> Endpoints,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

public record ServiceEndpoint(
    string Host,
    int Port,
    int Weight,
    bool IsActive
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
    int PageSize = 20
);