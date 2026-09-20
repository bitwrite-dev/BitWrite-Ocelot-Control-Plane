using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Application.UseCases.Publication;
using DomainPublication = BitWrite.OcelotControl.Domain.Aggregates.Publication.Publication;
using BitWrite.OcelotControl.Domain.Aggregates.Snapshot;
using BitWrite.OcelotControl.Domain.Aggregates.Gateway;
using BitWrite.OcelotControl.Domain.Events;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Domain.ValueObjects.Status;

namespace BitWrite.OcelotControl.Application.UseCases.Publication;

public class RollbackSnapshotCommandHandler
{
    private readonly ISnapshotRepository _snapshotRepository;
    private readonly IPublicationRepository _publicationRepository;
    private readonly IGatewayRepository _gatewayRepository;
    private readonly ISnapshotIntegrityVerifier _integrityVerifier;
    private readonly IDistributedLock _distributedLock;
    private readonly IRedisPublisher _redisPublisher;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public RollbackSnapshotCommandHandler(
        ISnapshotRepository snapshotRepository,
        IPublicationRepository publicationRepository,
        IGatewayRepository gatewayRepository,
        ISnapshotIntegrityVerifier integrityVerifier,
        IDistributedLock distributedLock,
        IRedisPublisher redisPublisher,
        IDomainEventDispatcher eventDispatcher)
    {
        _snapshotRepository = snapshotRepository;
        _publicationRepository = publicationRepository;
        _gatewayRepository = gatewayRepository;
        _integrityVerifier = integrityVerifier;
        _distributedLock = distributedLock;
        _redisPublisher = redisPublisher;
        _eventDispatcher = eventDispatcher;
    }

    public async Task<PublicationId> HandleAsync(RollbackSnapshotCommand command, CancellationToken cancellationToken = default)
    {
        // 1. Parse and verify target Snapshot exists
        if (!int.TryParse(command.TargetSnapshotVersion, out var versionInt))
        {
            throw new ArgumentException($"Invalid target snapshot version: {command.TargetSnapshotVersion}");
        }
        var targetVersion = SnapshotVersion.From(versionInt);

        var targetSnapshot = await _snapshotRepository.GetAsync(targetVersion, cancellationToken);
        if (targetSnapshot == null)
        {
            throw new InvalidOperationException($"Target snapshot {targetVersion} not found");
        }

        // 2. Verify target snapshot is valid
        var computedHash = _integrityVerifier.ComputeHash(targetSnapshot.Content);
        if (!targetSnapshot.VerifyIntegrity(computedHash))
        {
            throw new InvalidOperationException($"Target snapshot {targetVersion} integrity verification failed");
        }

        // 3. Get target gateways
        var targetGatewayIds = command.TargetGatewayIds?.Select(GatewayId.From).ToList() 
            ?? (await _gatewayRepository.GetAllAsync(cancellationToken)).Select(g => g.Id).ToList();

        if (targetGatewayIds.Count == 0)
        {
            throw new InvalidOperationException("No target gateways available for rollback");
        }

        // 4. Acquire rollback lock
        var lockResult = await _distributedLock.AcquireAsync(
            $"rollback:{targetVersion}",
            TimeSpan.FromMinutes(5),
            TimeSpan.FromSeconds(30),
            cancellationToken: cancellationToken);

        if (!lockResult.Success)
        {
            throw new InvalidOperationException($"Failed to acquire rollback lock: {lockResult.ErrorMessage}");
        }

        try
        {
            // 5. Get current active publication (if any)
            var currentPublication = await _publicationRepository.GetLatestAsync(cancellationToken);
            
            // 6. Create Publication aggregate for rollback
            var publication = DomainPublication.Start(
                targetVersion,
                command.InitiatedBy,
                targetGatewayIds,
                command.CorrelationId);

            // Mark as rollback context
            publication.Rollback(targetVersion, command.Reason);

            // Also mark target snapshot as rolled back
            targetSnapshot.Rollback(currentPublication != null ? currentPublication.SnapshotVersion : targetVersion, command.CorrelationId);

            // Persist snapshot with rollback event
            await _snapshotRepository.UpdateAsync(targetSnapshot, cancellationToken);

            // 7. Persist Publication
            await _publicationRepository.AddAsync(publication, cancellationToken);

            // 8. Atomic: Set ocelot:runtime:current = targetVersion
            await _redisPublisher.PublishAsync(
                "ocelot:runtime:current",
                new { Version = targetVersion.Value.ToString() },
                cancellationToken);

            // 9. Redis Pub/Sub: Notify gateways of rollback
            await _redisPublisher.PublishAsync(
                "ocelot:snapshot:rolled-back",
                new { Version = targetVersion.Value.ToString(), PublicationId = publication.Id.Value.ToString(), Reason = command.Reason },
                cancellationToken);

            // 10. Raise domain events
            foreach (var domainEvent in publication.DomainEvents)
            {
                await _eventDispatcher.DispatchAsync(domainEvent, cancellationToken);
            }

            foreach (var domainEvent in targetSnapshot.DomainEvents)
            {
                await _eventDispatcher.DispatchAsync(domainEvent, cancellationToken);
            }
            targetSnapshot.ClearDomainEvents();

            // 11. Raise audit event
            var auditEvent = new AuditRecorded(
                command.InitiatedBy,
                "RollbackSnapshot",
                "Publication",
                publication.Id.Value.ToString(),
                "Started"
            );
            await _eventDispatcher.DispatchAsync(auditEvent, cancellationToken);

            return publication.Id;
        }
        catch
        {
            // Release lock on failure
            if (lockResult.LockId != null)
            {
                await _distributedLock.ReleaseAsync($"rollback:{targetVersion}", lockResult.LockId, cancellationToken);
            }
            throw;
        }
    }
}