using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Application.UseCases.Snapshot;
using DomainSnapshot = BitWrite.OcelotControl.Domain.Aggregates.Snapshot.Snapshot;
using DomainGlobalConfig = BitWrite.OcelotControl.Domain.Services.GlobalConfiguration;
using BitWrite.OcelotControl.Domain.Events;
using BitWrite.OcelotControl.Domain.Services;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using System.Text.Json;

namespace BitWrite.OcelotControl.Application.UseCases.Snapshot;

public class CloneSnapshotCommandHandler
{
    private readonly ISnapshotRepository _snapshotRepository;
    private readonly ISnapshotVersionAllocator _versionAllocator;
    private readonly ISnapshotIntegrityVerifier _integrityVerifier;
    private readonly IConfigurationBuilder _configurationBuilder;
    private readonly IConfigurationCanonicalizer _canonicalizer;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public CloneSnapshotCommandHandler(
        ISnapshotRepository snapshotRepository,
        ISnapshotVersionAllocator versionAllocator,
        ISnapshotIntegrityVerifier integrityVerifier,
        IConfigurationBuilder configurationBuilder,
        IConfigurationCanonicalizer canonicalizer,
        IDomainEventDispatcher eventDispatcher)
    {
        _snapshotRepository = snapshotRepository;
        _versionAllocator = versionAllocator;
        _integrityVerifier = integrityVerifier;
        _configurationBuilder = configurationBuilder;
        _canonicalizer = canonicalizer;
        _eventDispatcher = eventDispatcher;
    }

    public async Task<CloneSnapshotResponse?> HandleAsync(CloneSnapshotCommand command, CancellationToken cancellationToken = default)
    {
        var sourceSnapshot = await _snapshotRepository.GetAsync(command.Version, cancellationToken);
        if (sourceSnapshot == null)
            return null;

        // Allocate new version
        var newVersion = _versionAllocator.AllocateNext();

        // Deserialize content to OcelotConfiguration for canonicalization
        var ocelotConfig = JsonSerializer.Deserialize<OcelotConfiguration>(sourceSnapshot.Content);
        if (ocelotConfig == null)
        {
            throw new InvalidOperationException("Failed to deserialize snapshot content");
        }

        // Canonicalize and hash using the OcelotConfiguration object
        var canonicalJson = _canonicalizer.CanonicalizeJson(ocelotConfig);
        var hash = _configurationBuilder.CalculateConfigurationHash(ocelotConfig);

        // Create new snapshot
        var clonedSnapshot = DomainSnapshot.Create(
            canonicalJson,
            hash,
            newVersion,
            command.InitiatedBy
        );

        // Persist
        await _snapshotRepository.AddAsync(clonedSnapshot, cancellationToken);

        // Dispatch domain event
        var createdEvent = new SnapshotCreated(newVersion, hash, command.InitiatedBy);
        await _eventDispatcher.DispatchAsync(createdEvent, cancellationToken);

        // Dispatch audit event
        var auditEvent = new AuditRecorded(
            command.InitiatedBy,
            "CloneSnapshot",
            "Snapshot",
            newVersion.Value.ToString(),
            "Success"
        );
        await _eventDispatcher.DispatchAsync(auditEvent, cancellationToken);

        return new CloneSnapshotResponse(
            newVersion,
            command.Version,
            command.NewName
        );
    }
}