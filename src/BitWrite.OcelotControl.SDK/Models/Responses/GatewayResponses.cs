using System.ComponentModel.DataAnnotations;

namespace BitWrite.OcelotControl.SDK.Models.Responses;

public record GatewayResponse(
    string Id,
    string Name,
    string? Description,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

public record GatewayListResponse(
    List<GatewayResponse> Gateways,
    int TotalCount,
    int Page,
    int PageSize
);