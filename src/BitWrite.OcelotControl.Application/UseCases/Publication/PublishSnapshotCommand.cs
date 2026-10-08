using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;

namespace BitWrite.OcelotControl.Application.UseCases.Publication;

/// <summary>
/// Publish a sealed Snapshot to the gateways that should serve it.
/// </summary>
/// <remarks>
/// The environment is part of the command rather than resolved from the request at
/// the edge, because the controller has already checked that the request's own
/// environment matches the publication's: they are the same environment by the time
/// the command reaches here, and the handler does not have to re-ask or re-check.
/// </remarks>
public record PublishSnapshotCommand(
    string SnapshotVersion,
    string InitiatedBy,
    EnvironmentName Environment,
    string CorrelationId = "",
    IReadOnlyList<string>? TargetGatewayIds = null
);
