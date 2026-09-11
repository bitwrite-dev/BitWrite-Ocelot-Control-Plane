namespace BitWrite.OcelotControl.Application.UseCases.Publication;

public record RollbackSnapshotCommand(
    string TargetSnapshotVersion,
    string InitiatedBy,
    string CorrelationId = "",
    string Reason = "Rollback requested",
    IReadOnlyList<string>? TargetGatewayIds = null
);