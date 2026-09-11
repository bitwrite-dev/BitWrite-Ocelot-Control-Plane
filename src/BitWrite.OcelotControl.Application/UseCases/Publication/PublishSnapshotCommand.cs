namespace BitWrite.OcelotControl.Application.UseCases.Publication;

public record PublishSnapshotCommand(
    string SnapshotVersion,
    string InitiatedBy,
    string CorrelationId = "",
    IReadOnlyList<string>? TargetGatewayIds = null
);