using BitWrite.OcelotControl.Domain.ValueObjects.Identity;

namespace BitWrite.OcelotControl.Application.UseCases.Route;

public record DeleteRouteCommand(
    RouteId Id,
    string InitiatedBy = "",
    string CorrelationId = ""
);