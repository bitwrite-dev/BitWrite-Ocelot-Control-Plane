using System.ComponentModel.DataAnnotations;

namespace BitWrite.OcelotControl.Api.DTOs;

public record CreateGatewayRequest(
    [Required][MaxLength(200)] string Name,
    [MaxLength(1000)] string? Description
);

public record UpdateGatewayRequest(
    [MaxLength(200)] string? Name,
    [MaxLength(1000)] string? Description
);

public record UpdateGatewayStatusRequest(
    [Required] string Status
);

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