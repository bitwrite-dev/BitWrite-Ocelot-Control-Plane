using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Domain.ValueObjects.Status;

namespace BitWrite.OcelotControl.Application.UseCases.Gateway;

public record UpdateGatewayStatusCommand(
    GatewayId Id,
    RuntimeStatus Status,
    string InitiatedBy,
    string CorrelationId = ""
);