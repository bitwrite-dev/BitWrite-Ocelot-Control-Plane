using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;

namespace BitWrite.OcelotControl.Application.UseCases.Service;

public record UpdateServiceCommand(
    ServiceId Id,
    string? Name = null,
    string? Description = null,
    IReadOnlyList<DownstreamTarget>? DownstreamTargets = null,
    string InitiatedBy = "",
    string CorrelationId = ""
);