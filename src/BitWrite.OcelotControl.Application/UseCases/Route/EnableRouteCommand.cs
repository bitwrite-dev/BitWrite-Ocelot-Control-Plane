using BitWrite.OcelotControl.Domain.ValueObjects.Identity;

namespace BitWrite.OcelotControl.Application.UseCases.Route;

public record EnableRouteCommand(
    RouteId Id,
    string InitiatedBy = "",
    string CorrelationId = ""
);