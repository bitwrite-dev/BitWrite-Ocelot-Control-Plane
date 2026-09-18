using BitWrite.OcelotControl.Domain.ValueObjects.Identity;

namespace BitWrite.OcelotControl.Application.UseCases.Route;

public record EnableRouteCommand(
    RouteId RouteId,
    string InitiatedBy = "",
    string CorrelationId = ""
);
