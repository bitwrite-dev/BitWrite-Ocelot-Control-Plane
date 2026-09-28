using BitWrite.OcelotControl.Application.Interfaces;
using SystemSettingsAggregate = BitWrite.OcelotControl.Domain.Aggregates.SystemSettings.SystemSettings;
using OcelotVersionCatalog = BitWrite.OcelotControl.Domain.Aggregates.SystemSettings.OcelotVersionCatalog;
using BitWrite.OcelotControl.Domain.Events;
using BitWrite.OcelotControl.Domain.Exceptions;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using OcelotVersion = BitWrite.OcelotControl.Domain.ValueObjects.Configuration.OcelotVersion;

namespace BitWrite.OcelotControl.Application.UseCases.SystemSettings;

/// <summary>
/// First-run setup and the settings that can change afterwards.
/// </summary>
public class SystemSettingsCommandHandler
{
    private readonly ISystemSettingsRepository _repository;
    private readonly IDomainEventDispatcher _eventDispatcher;

    public SystemSettingsCommandHandler(
        ISystemSettingsRepository repository,
        IDomainEventDispatcher eventDispatcher)
    {
        _repository = repository;
        _eventDispatcher = eventDispatcher;
    }

    public async Task<SystemSettingsResponse> HandleAsync(
        GetSystemSettingsQuery query,
        CancellationToken cancellationToken = default)
    {
        var settings = await _repository.GetAsync(cancellationToken);
        return Map(settings);
    }

    /// <summary>
    /// The one-time choice of Ocelot version.
    /// </summary>
    /// <remarks>
    /// The version is applied before the write rather than after, so a refused
    /// choice never touches storage. The write itself is create-if-absent, so a
    /// second caller who passes the same version is told the truth — the settings
    /// are already at that version — instead of being told the version is
    /// immutable and leaving the caller to guess whether their first attempt
    /// landed.
    /// </remarks>
    public async Task<SystemSettingsResponse> HandleAsync(
        CompleteFirstRunCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.OcelotVersion))
            throw new DomainException("An Ocelot version must be chosen", "MISSING_OCELOT_VERSION");

        OcelotVersion version;
        try
        {
            version = OcelotVersion.Parse(command.OcelotVersion);
        }
        catch (DomainException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Parse throws a FormatException on a non-numeric part, which would
            // otherwise surface as a 500.
            throw new DomainException(
                $"Invalid Ocelot version: {command.OcelotVersion}", "INVALID_OCELOT_VERSION");
        }

        var settings = await _repository.GetAsync(cancellationToken);

        if (settings.IsInitialised)
        {
            if (settings.OcelotVersion!.CompareTo(version) == 0)
            {
                // The same answer to the same question. Reporting the version as
                // immutable here would be true and useless.
                return Map(settings);
            }

            settings.ChooseOcelotVersion(version, command.InitiatedBy, command.CorrelationId);
        }
        else
        {
            settings.ChooseOcelotVersion(version, command.InitiatedBy, command.CorrelationId);
            ApplyOptionalSettings(settings, command);
        }

        if (settings.DomainEvents.Count > 0)
        {
            var events = settings.DomainEvents.ToList();
            settings.ClearDomainEvents();
            await _eventDispatcher.DispatchAsync(events, cancellationToken);
        }

        if (!settings.IsInitialised)
        {
            // Unreachable in practice; kept so a future path cannot write settings
            // with no version, which would leave the builder refusing every read.
            throw new DomainException("First run did not result in a chosen version", "MISSING_OCELOT_VERSION");
        }

        await _repository.UpdateAsync(settings, cancellationToken);
        return Map(settings);
    }

    public async Task<SystemSettingsResponse> HandleAsync(
        UpdateSystemSettingsCommand command,
        CancellationToken cancellationToken = default)
    {
        var settings = await _repository.GetAsync(cancellationToken);

        if (command.PollIntervalSeconds is { } poll)
            settings.SetPollInterval(poll, command.CorrelationId);

        if (command.AuditLogRetentionDays is { } retention)
            settings.SetAuditLogRetention(retention, command.CorrelationId);

        if (command.SnapshotRetentionCount is { } snapshots)
            settings.SetSnapshotRetention(snapshots, command.CorrelationId);

        await _repository.UpdateAsync(settings, cancellationToken);

        await _eventDispatcher.DispatchAsync(
            new AuditRecorded(
                command.InitiatedBy,
                "UpdateSystemSettings",
                "SystemSettings",
                settings.Id.ToString(),
                "Success"),
            cancellationToken);

        return Map(settings);
    }

    private static void ApplyOptionalSettings(SystemSettingsAggregate settings, CompleteFirstRunCommand command)
    {
        if (command.PollIntervalSeconds is { } poll)
            settings.SetPollInterval(poll, command.CorrelationId);

        if (command.AuditLogRetentionDays is { } retention)
            settings.SetAuditLogRetention(retention, command.CorrelationId);

        if (command.SnapshotRetentionCount is { } snapshots)
            settings.SetSnapshotRetention(snapshots, command.CorrelationId);
    }

    private static SystemSettingsResponse Map(SystemSettingsAggregate settings) => new(
        settings.Id.ToString(),
        settings.OcelotVersion?.ToString(),
        settings.OcelotVersionSelectedAt,
        settings.OcelotVersionSelectedBy,
        settings.PollIntervalSeconds,
        settings.AuditLogRetentionDays,
        settings.SnapshotRetentionCount,
        settings.IsInitialised,
        OcelotVersionCatalog.EmittableVersions().Select(version => version.ToString()).ToList(),
        settings.CreatedAt,
        settings.UpdatedAt);
}
