using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;

namespace BitWrite.OcelotControl.Application.UseCases.Service;

public record CreateServiceCommand(
    string Name,
    string? Description = null,
    IReadOnlyList<DownstreamTarget>? DownstreamTargets = null,
    string InitiatedBy = "",
    string CorrelationId = ""
);