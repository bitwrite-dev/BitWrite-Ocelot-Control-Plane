using BitWrite.OcelotControl.Domain.ValueObjects.Identity;

namespace BitWrite.OcelotControl.Application.UseCases.Service;

public record DeleteServiceCommand(
    ServiceId Id,
    string InitiatedBy = "",
    string CorrelationId = ""
);