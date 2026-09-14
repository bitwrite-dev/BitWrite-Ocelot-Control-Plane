using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Domain.Aggregates.RuntimeInstance;
using BitWrite.OcelotControl.Domain.Aggregates.Snapshot;
using BitWrite.OcelotControl.Domain.Services;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Domain.ValueObjects.Status;
using BitWrite.OcelotControl.Infrastructure.Adapters;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace BitWrite.OcelotControl.Runtime.Adapters;

public class RuntimeAdapter : BackgroundService
{
    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly ISnapshotRepository _snapshotRepository;
    private readonly IRuntimeInstanceRepository _runtimeInstanceRepository;
    private readonly ISnapshotIntegrityVerifier _integrityVerifier;
    private readonly IOcelotCapabilityResolver _capabilityResolver;
    private readonly IOcelotConfigApplier _configApplier;
    private readonly IDomainEventDispatcher _eventDispatcher;
    private readonly ILogger<RuntimeAdapter> _logger;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(30);
    private readonly TimeSpan _retryInterval = TimeSpan.FromSeconds(5);
    private GatewayId _gatewayId;
    private SnapshotVersion? _currentVersion;
    private SnapshotVersion? _lastKnownGoodVersion;
    private string? _lastKnownGoodConfig;

    public RuntimeAdapter(
        IConnectionMultiplexer connectionMultiplexer,
        ISnapshotRepository snapshotRepository,
        IRuntimeInstanceRepository runtimeInstanceRepository,
        ISnapshotIntegrityVerifier integrityVerifier,
        IOcelotCapabilityResolver capabilityResolver,
        IOcelotConfigApplier configApplier,
        IDomainEventDispatcher eventDispatcher,
        ILogger<RuntimeAdapter> logger)
    {
        _connectionMultiplexer = connectionMultiplexer;
        _snapshotRepository = snapshotRepository;
        _runtimeInstanceRepository = runtimeInstanceRepository;
        _integrityVerifier = integrityVerifier;
        _capabilityResolver = capabilityResolver;
        _configApplier = configApplier;
        _eventDispatcher = eventDispatcher;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _gatewayId = GatewayId.New(); // In real implementation, this would come from config
        
        _logger.LogInformation("RuntimeAdapter started for gateway {GatewayId}", _gatewayId);

        // Subscribe to version notifications
        await SubscribeToVersionNotificationsAsync(stoppingToken);

        // Main reconciliation loop
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ReconcileAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during reconciliation for gateway {GatewayId}", _gatewayId);
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }

        _logger.LogInformation("RuntimeAdapter stopped for gateway {GatewayId}", _gatewayId);
    }

    private async Task SubscribeToVersionNotificationsAsync(CancellationToken cancellationToken)
    {
        var subscriber = _connectionMultiplexer.GetSubscriber();
        
        await subscriber.SubscribeAsync("ocelot:snapshot:published", async (channel, message) =>
        {
            _logger.LogInformation("Received version notification: {Message}", message);
            await HandleVersionNotificationAsync(message.ToString(), cancellationToken);
        });

        await subscriber.SubscribeAsync("ocelot:snapshot:rolled-back", async (channel, message) =>
        {
            _logger.LogInformation("Received rollback notification: {Message}", message);
            await HandleRollbackNotificationAsync(message.ToString(), cancellationToken);
        });
    }

    private async Task HandleVersionNotificationAsync(string message, CancellationToken cancellationToken)
    {
        try
        {
            var notification = System.Text.Json.JsonSerializer.Deserialize<VersionNotification>(message);
            if (notification != null && int.TryParse(notification.Version, out var versionInt))
            {
                var version = SnapshotVersion.From(versionInt);
                await ApplySnapshotAsync(version, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle version notification");
        }
    }

    private async Task HandleRollbackNotificationAsync(string message, CancellationToken cancellationToken)
    {
        try
        {
            var notification = System.Text.Json.JsonSerializer.Deserialize<RollbackNotification>(message);
            if (notification != null && int.TryParse(notification.Version, out var versionInt))
            {
                var version = SnapshotVersion.From(versionInt);
                await ApplySnapshotAsync(version, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle rollback notification");
        }
    }

    private async Task ReconcileAsync(CancellationToken cancellationToken)
    {
        // Poll current version from Redis
        var db = _connectionMultiplexer.GetDatabase();
        var currentVersionStr = await db.StringGetAsync("ocelot:runtime:current");
        
        if (!currentVersionStr.IsNullOrEmpty && int.TryParse(currentVersionStr, out var versionInt))
        {
            var version = SnapshotVersion.From(versionInt);
            
            if (_currentVersion != version)
            {
                _logger.LogInformation("Version change detected: {CurrentVersion} -> {NewVersion}", _currentVersion, version);
                await ApplySnapshotAsync(version, cancellationToken);
            }
        }

        // Update heartbeat
        await UpdateHeartbeatAsync(cancellationToken);
    }

    private async Task ApplySnapshotAsync(SnapshotVersion version, CancellationToken cancellationToken)
    {
        var retryCount = 0;
        const int maxRetries = 3;

        while (retryCount <= maxRetries)
        {
            try
            {
                _logger.LogInformation("Applying snapshot version {Version} (attempt {Attempt}/{MaxRetries})", 
                    version, retryCount + 1, maxRetries + 1);

                // 1. Retrieve Snapshot
                var snapshot = await _snapshotRepository.GetAsync(version, cancellationToken);
                if (snapshot == null)
                {
                    throw new InvalidOperationException($"Snapshot {version} not found");
                }

                // 2. Verify integrity
                var computedHash = _integrityVerifier.ComputeHash(snapshot.Content);
                if (!snapshot.VerifyIntegrity(computedHash))
                {
                    throw new InvalidOperationException($"Snapshot {version} integrity verification failed");
                }

                // 3. Check Ocelot version compatibility
                var ocelotVersion = OcelotVersion.Parse("20.0.0"); // In real implementation, get from config
                var features = ExtractFeaturesFromSnapshot(snapshot.Content);
                var capabilityErrors = _capabilityResolver.IsCapabilitySupported("", ocelotVersion);
                
                // For now, just check if any features are incompatible
                foreach (var feature in features)
                {
                    if (!_capabilityResolver.IsCapabilitySupported(feature, ocelotVersion))
                    {
                        throw new InvalidOperationException($"Feature {feature} not supported by Ocelot {ocelotVersion}");
                    }
                }

                // 4. Apply configuration atomically
                await _configApplier.ApplyAsync(snapshot.Content, cancellationToken);

                // 5. Store as known-good version
                _lastKnownGoodVersion = _currentVersion;
                _lastKnownGoodConfig = _currentVersion != null ? snapshot.Content : null;

                // 6. Update current version
                _currentVersion = version;

                // 7. Update RuntimeInstance state
                var runtimeInstance = await _runtimeInstanceRepository.GetAsync(_gatewayId, cancellationToken);
                if (runtimeInstance == null)
                {
                    runtimeInstance = RuntimeInstance.Register(_gatewayId, Array.Empty<string>(), string.Empty);
                }
                runtimeInstance.RecordHeartbeat();
                runtimeInstance.RecordConfigApplied(version);
                await _runtimeInstanceRepository.UpdateAsync(runtimeInstance, cancellationToken);

                // 8. Report activation result
                await ReportActivationResultAsync(version, true, null, cancellationToken);

                _logger.LogInformation("Successfully applied snapshot version {Version}", version);
                return;
            }
            catch (Exception ex)
            {
                retryCount++;
                _logger.LogWarning(ex, "Failed to apply snapshot {Version} (attempt {Attempt}/{MaxRetries})", 
                    version, retryCount, maxRetries + 1);

                if (retryCount > maxRetries)
                {
                    // Rollback to known-good version
                    await RollbackToKnownGoodAsync(cancellationToken);
                    await ReportActivationResultAsync(version, false, ex.Message, cancellationToken);
                    return;
                }

                await Task.Delay(_retryInterval * retryCount, cancellationToken);
            }
        }
    }

    private async Task RollbackToKnownGoodAsync(CancellationToken cancellationToken)
    {
        if (_lastKnownGoodVersion != null && _lastKnownGoodConfig != null)
        {
            _logger.LogWarning("Rolling back to known-good version {Version}", _lastKnownGoodVersion);
            
            try
            {
                await _configApplier.ApplyAsync(_lastKnownGoodConfig, cancellationToken);
                _currentVersion = _lastKnownGoodVersion;

                var runtimeInstance = await _runtimeInstanceRepository.GetAsync(_gatewayId, cancellationToken);
                if (runtimeInstance != null)
                {
                    runtimeInstance.RecordConfigApplied(_lastKnownGoodVersion!);
                    await _runtimeInstanceRepository.UpdateAsync(runtimeInstance, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to rollback to known-good version");
            }
        }
    }

    private async Task ReportActivationResultAsync(SnapshotVersion version, bool success, string? error, CancellationToken cancellationToken)
    {
        var db = _connectionMultiplexer.GetDatabase();
        var result = new
        {
            GatewayId = _gatewayId.Value.ToString(),
            Version = version.Value.ToString(),
            Success = success,
            Error = error,
            Timestamp = DateTimeOffset.UtcNow
        };

        var json = System.Text.Json.JsonSerializer.Serialize(result);
        await db.ListRightPushAsync("ocelot:gateway:activation:results", json);
    }

    private async Task UpdateHeartbeatAsync(CancellationToken cancellationToken)
    {
        var db = _connectionMultiplexer.GetDatabase();
        var heartbeat = new
        {
            GatewayId = _gatewayId.Value.ToString(),
            Version = _currentVersion?.Value.ToString(),
            Timestamp = DateTimeOffset.UtcNow
        };

        var json = System.Text.Json.JsonSerializer.Serialize(heartbeat);
        await db.StringSetAsync($"ocelot:runtime:gateway:{_gatewayId.Value}", json);
    }

    private List<string> ExtractFeaturesFromSnapshot(string content)
    {
        // Simplified - in real implementation, parse the snapshot content
        return new List<string>();
    }

    private record VersionNotification(string Version, string PublicationId);
    private record RollbackNotification(string Version, string PublicationId, string Reason);
}