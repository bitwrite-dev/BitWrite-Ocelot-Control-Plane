using BitWrite.OcelotControl.Domain.ValueObjects.Identity;

namespace BitWrite.OcelotControl.Application.UseCases.Gateway;

public record UpdateGatewayCommand(
    GatewayId Id,
    string? Name,
    string? Description,
    string InitiatedBy,
    string CorrelationId = ""
);