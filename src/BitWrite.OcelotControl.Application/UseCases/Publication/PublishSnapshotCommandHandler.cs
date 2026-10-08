using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Application.UseCases.Publication;
using BitWrite.OcelotControl.Domain.Aggregates.Gateway;
using BitWrite.OcelotControl.Domain.Aggregates.Snapshot;
using BitWrite.OcelotControl.Domain.Events;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Domain.ValueObjects.Status;
using DomainPublication = BitWrite.OcelotControl.Domain.Aggregates.Publication.Publication;

namespace BitWrite.OcelotControl.Application.UseCases.Publication;

public class PublishSnapshotCommandHandler
{
    private readonly ISnapshotRepository _snapshotRepository;
    private readonly IPublicationRepository _publicationRepository;
    private readonly IGatewayRepository _gatewayRepository;
    private readonly ISnapshotIntegrityVerifier _integrityVerifier;
    private readonly IDistributedLock _distributedLock;
    private readonly IRedisPublisher _redisPublisher;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public PublishSnapshotCommandHandler(
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

    public async Task<PublicationId> HandleAsync(PublishSnapshotCommand command, CancellationToken cancellationToken = default)
    {
        // 1. Parse and verify Snapshot exists
        if (!int.TryParse(command.SnapshotVersion, out var versionInt))
        {
            throw new ArgumentException($"Invalid snapshot version: {command.SnapshotVersion}");
        }
        var snapshotVersion = SnapshotVersion.From(versionInt);

        var snapshot = await _snapshotRepository.GetAsync(snapshotVersion, cancellationToken);
        if (snapshot == null)
        {
            throw new InvalidOperationException($"Snapshot {snapshotVersion} not found");
        }

        // 2. Verify status allows publishing (Ready or Published)
        if (snapshot.Status != SnapshotStatus.Ready && snapshot.Status != SnapshotStatus.Published)
        {
            throw new InvalidOperationException($"Snapshot {snapshotVersion} cannot be published. Current status: {snapshot.Status}");
        }

        // 3. Verify snapshot integrity
        var computedHash = _integrityVerifier.ComputeHash(snapshot.Content);
        if (!snapshot.VerifyIntegrity(computedHash))
        {
            throw new InvalidOperationException($"Snapshot {snapshotVersion} integrity verification failed");
        }

        // 4. Get target gateways
        var targetGatewayIds = command.TargetGatewayIds?.Select(GatewayId.From).ToList()
            ?? (await _gatewayRepository.GetAllAsync(cancellationToken)).Select(g => g.Id).ToList();

        if (targetGatewayIds.Count == 0)
        {
            throw new InvalidOperationException("No target gateways available for publication");
        }

        // 4.5. Every target gateway has to be in the environment the publication is for.
        //
        // A publication is for one environment and is delivered to the gateways that
        // serve it. Handing a production publication to a development gateway would
        // put production's routes behind a development name, and neither side would
        // report it — the gateway would simply start serving the wrong environment.
        var gateways = await _gatewayRepository.GetAllAsync(cancellationToken);
        var mismatched = gateways
            .Where(gateway => targetGatewayIds.Contains(gateway.Id))
            .Where(gateway => gateway.Environment != command.Environment)
            .ToList();

        if (mismatched.Count > 0)
        {
            throw new InvalidOperationException(
                $"Publication is for environment '{command.Environment}' but these gateways serve another: " +
                string.Join(", ", mismatched.Select(g => g.Id.Value.ToString())));
        }

        // 5. Acquire publish lock
        var lockResult = await _distributedLock.AcquireAsync(
            $"publish:{snapshotVersion}",
            TimeSpan.FromMinutes(5),
            TimeSpan.FromSeconds(30),
            cancellationToken: cancellationToken);

        if (!lockResult.Success)
        {
            throw new InvalidOperationException($"Failed to acquire publish lock: {lockResult.ErrorMessage}");
        }

        try
        {
            // 6. Create Publication aggregate
            var publication = DomainPublication.Start(
                snapshotVersion,
                command.InitiatedBy,
                targetGatewayIds,
                command.CorrelationId);

            // 7. Persist Publication
            await _publicationRepository.AddAsync(publication, cancellationToken);

            // 8. Atomic: Set ocelot:runtime:current:{env} = version (via Redis)
            await _redisPublisher.PublishAsync(
                $"ocelot:runtime:current:{command.Environment.Value}",
                new { Version = snapshotVersion.Value.ToString(), Environment = command.Environment.Value },
                cancellationToken);

            // 9. Redis Pub/Sub: Notify gateways of new version
            await _redisPublisher.PublishAsync(
                "ocelot:snapshot:published",
                new
                {
                    Version = snapshotVersion.Value.ToString(),
                    Environment = command.Environment.Value,
                    PublicationId = publication.Id.Value.ToString()
                },
                cancellationToken);

            // 10. Raise domain events
            foreach (var domainEvent in publication.DomainEvents)
            {
                await _eventDispatcher.DispatchAsync(domainEvent, cancellationToken);
            }

            // 11. Raise audit event
            var auditEvent = new AuditRecorded(
                command.InitiatedBy,
                "PublishSnapshot",
                "Publication",
                publication.Id.Value.ToString(),
                "Started"
            );
            await _eventDispatcher.DispatchAsync(auditEvent, cancellationToken);

            // 12. Raise integration event
            var integrationEvent = new SnapshotPublishedIntegrationEvent(snapshotVersion, DateTimeOffset.UtcNow);
            await _eventDispatcher.DispatchAsync(integrationEvent, cancellationToken);

            return publication.Id;
        }
        catch
        {
            // Release lock on failure
            if (lockResult.LockId != null)
            {
                await _distributedLock.ReleaseAsync($"publish:{snapshotVersion}", lockResult.LockId, cancellationToken);
            }
            throw;
        }
    }
}
