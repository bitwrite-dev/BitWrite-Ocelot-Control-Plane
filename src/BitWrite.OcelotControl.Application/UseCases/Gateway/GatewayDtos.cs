using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Domain.ValueObjects.Status;

namespace BitWrite.OcelotControl.Application.UseCases.Gateway;

public record GatewayResponse(
    GatewayId Id,
    string Name,
    string? Description,
    RuntimeStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

public record GatewayListResponse(
    IReadOnlyList<GatewayResponse> Gateways,
    int TotalCount,
    int Page,
    int PageSize
);