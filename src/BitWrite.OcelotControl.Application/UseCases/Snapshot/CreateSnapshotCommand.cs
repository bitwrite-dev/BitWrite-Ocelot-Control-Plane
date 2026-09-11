namespace BitWrite.OcelotControl.Application.UseCases.Snapshot;

public record CreateSnapshotCommand(
    string InitiatedBy,
    string CorrelationId = ""
);