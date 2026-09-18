using BitWrite.OcelotControl.Domain.ValueObjects.Identity;

namespace BitWrite.OcelotControl.Application.UseCases.Route;

public record DisableRouteCommand(
    RouteId Id,
    string InitiatedBy = "",
    string CorrelationId = ""
);