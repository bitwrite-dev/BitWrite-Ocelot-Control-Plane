using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;

namespace BitWrite.OcelotControl.Application.UseCases.Publication;

/// <summary>
/// Roll a published Snapshot back to an earlier sealed version.
/// </summary>
/// <remarks>
/// Like a publication, a rollback names the environment it serves. Routes, services and
/// snapshots are stored per environment, and a rollback that named none would roll
/// back one environment's gateways while speaking to another's.
/// </remarks>
public record RollbackSnapshotCommand(
    string TargetSnapshotVersion,
    string InitiatedBy,
    EnvironmentName Environment,
    string CorrelationId = "",
    string Reason = "Rollback requested",
    IReadOnlyList<string>? TargetGatewayIds = null
);
