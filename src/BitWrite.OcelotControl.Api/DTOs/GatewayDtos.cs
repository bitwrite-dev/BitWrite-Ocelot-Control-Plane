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
    DateTimeOffset UpdatedAt,
    /// <summary>
    /// When the gateway last reported in, or null if it never has.
    /// </summary>
    /// <remarks>
    /// Carried with the status because the status is a label the control plane
    /// records and nothing updates it on its own. A gateway that has not reported
    /// in hours is not the thing its status says.
    /// </remarks>
    DateTimeOffset? LastHeartbeat = null
);

public record GatewayListResponse(
    List<GatewayResponse> Gateways,
    int TotalCount,
    int Page,
    int PageSize
);