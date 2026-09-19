using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Application.UseCases.Runtime;
using BitWrite.OcelotControl.Domain.Aggregates.RuntimeInstance;
using BitWrite.OcelotControl.Domain.Events;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Domain.ValueObjects.Status;

namespace BitWrite.OcelotControl.Application.UseCases.Runtime;

public class ReconcileGatewayCommandHandler
{
    private readonly IRuntimeInstanceRepository _runtimeInstanceRepository;
    private readonly ISnapshotRepository _snapshotRepository;
    private readonly IOcelotConfigApplier _configApplier;
    private readonly ISnapshotIntegrityVerifier _integrityVerifier;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public ReconcileGatewayCommandHandler(
        IRuntimeInstanceRepository runtimeInstanceRepository,
        ISnapshotRepository snapshotRepository,
        IOcelotConfigApplier configApplier,
        ISnapshotIntegrityVerifier integrityVerifier,
        IDomainEventDispatcher eventDispatcher)
    {
        _runtimeInstanceRepository = runtimeInstanceRepository;
        _snapshotRepository = snapshotRepository;
        _configApplier = configApplier;
        _integrityVerifier = integrityVerifier;
        _eventDispatcher = eventDispatcher;
    }

    public async Task<ReconcileGatewayResponse> HandleAsync(ReconcileGatewayCommand command, CancellationToken cancellationToken = default)
    {
        // Get runtime instance
        var runtimeInstance = await _runtimeInstanceRepository.GetAsync(command.GatewayId, cancellationToken);
        if (runtimeInstance == null)
        {
            return new ReconcileGatewayResponse(
                command.GatewayId,
                command.TargetVersion,
                false,
                $"Runtime instance for gateway {command.GatewayId} not found",
                DateTimeOffset.UtcNow
            );
        }

        // Get target snapshot
        var snapshot = await _snapshotRepository.GetAsync(command.TargetVersion, cancellationToken);
        if (snapshot == null)
        {
            return new ReconcileGatewayResponse(
                command.GatewayId,
                command.TargetVersion,
                false,
                $"Snapshot {command.TargetVersion} not found",
                DateTimeOffset.UtcNow
            );
        }

        // Verify snapshot integrity
        var computedHash = _integrityVerifier.ComputeHash(snapshot.Content);
        if (!snapshot.VerifyIntegrity(computedHash))
        {
            return new ReconcileGatewayResponse(
                command.GatewayId,
                command.TargetVersion,
                false,
                $"Snapshot {command.TargetVersion} integrity verification failed",
                DateTimeOffset.UtcNow
            );
        }

        try
        {
            // Apply configuration
            await _configApplier.ApplyAsync(snapshot.Content, cancellationToken);

            // Update runtime instance
            runtimeInstance.RecordConfigApplied(command.TargetVersion);
            runtimeInstance.MarkSynchronized(command.TargetVersion);
            await _runtimeInstanceRepository.UpdateAsync(runtimeInstance, cancellationToken);

            // Dispatch domain events
            var domainEvent = new GatewayConfigurationApplied(command.GatewayId, command.TargetVersion);
            await _eventDispatcher.DispatchAsync(domainEvent, cancellationToken);

            // Dispatch audit event
            var auditEvent = new AuditRecorded(
                command.InitiatedBy,
                "ReconcileGateway",
                "RuntimeInstance",
                command.GatewayId.Value.ToString(),
                "Success"
            );
            await _eventDispatcher.DispatchAsync(auditEvent, cancellationToken);

            return new ReconcileGatewayResponse(
                command.GatewayId,
                command.TargetVersion,
                true,
                null,
                DateTimeOffset.UtcNow
            );
        }
        catch (Exception ex)
        {
            return new ReconcileGatewayResponse(
                command.GatewayId,
                command.TargetVersion,
                false,
                ex.Message,
                DateTimeOffset.UtcNow
            );
        }
    }
}