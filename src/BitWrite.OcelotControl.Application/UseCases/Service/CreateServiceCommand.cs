using BitWrite.OcelotControl.Domain.ValueObjects.Identity;

namespace BitWrite.OcelotControl.Application.UseCases.Service;

public record CreateServiceCommand(
    string Name,
    string? Description = null,
    string InitiatedBy = "",
    string CorrelationId = ""
);