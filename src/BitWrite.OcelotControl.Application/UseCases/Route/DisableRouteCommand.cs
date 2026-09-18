using BitWrite.OcelotControl.Domain.ValueObjects.Identity;

namespace BitWrite.OcelotControl.Application.UseCases.Route;

public record DisableRouteCommand(
    RouteId RouteId,
    string InitiatedBy = "",
    string CorrelationId = ""
);
