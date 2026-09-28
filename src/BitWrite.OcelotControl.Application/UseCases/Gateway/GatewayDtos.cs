using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Domain.ValueObjects.Status;

namespace BitWrite.OcelotControl.Application.UseCases.Gateway;

/// <param name="LastHeartbeat">
/// When the gateway last reported in, or null if it never has.
/// </param>
/// <remarks>
/// Carried alongside <see cref="Status"/> because the status is a label the
/// control plane records and nothing updates it on its own. A gateway whose last
/// report was hours ago is not the thing its status says, and the two together
/// are what let an operator judge it.
/// </remarks>
public record GatewayResponse(
    GatewayId Id,
    string Name,
    string? Description,
    RuntimeStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? LastHeartbeat = null
);

public record GatewayListResponse(
    IReadOnlyList<GatewayResponse> Gateways,
    int TotalCount,
    int Page,
    int PageSize
);