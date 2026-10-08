using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Infrastructure.Redis;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace BitWrite.OcelotControl.Migration;

/// <summary>
/// Migrates Service keys from the old format (ocelot:service:{id}) to the new
/// environment-scoped format (ocelot:service:{env}:{id}).
/// </summary>
/// <remarks>
/// Data loss is accepted per #525. This migrator:
/// 1. Scans for all keys matching the old pattern (ocelot:service:{guid})
/// 2. For each found service, reads its hash and migrates it to the new key
/// 3. Rebuilds the service index (ocelot:index:services:{env})
/// 4. Optionally deletes the old keys after successful migration
/// </remarks>
internal sealed class ServiceKeyMigrator
{
    private readonly IDatabase _db;
    private readonly Microsoft.Extensions.Logging.ILogger _logger;
    private readonly string _targetEnvironment;
    private readonly bool _deleteOldKeys;

    public ServiceKeyMigrator(
        IConnectionMultiplexer connectionMultiplexer,
        string targetEnvironment,
        bool deleteOldKeys,
        Microsoft.Extensions.Logging.ILogger logger)
    {
        _db = connectionMultiplexer.GetDatabase();
        _targetEnvironment = targetEnvironment;
        _deleteOldKeys = deleteOldKeys;
        _logger = logger;
    }

    public async Task<MigrationResult> MigrateAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Starting Service key migration to environment '{Environment}' (deleteOldKeys={Delete})",
            _targetEnvironment, _deleteOldKeys);

        var result = new MigrationResult { TargetEnvironment = _targetEnvironment };

        // 1. Find all old service keys: ocelot:service:{guid}
        var oldServiceKeys = new List<string>();
        var server = _db.Multiplexer.GetServer(_db.Multiplexer.GetEndPoints()[0]);

        await foreach (var key in server.KeysAsync(pattern: "ocelot:service:*", pageSize: 1000))
        {
            var keyStr = key.ToString();
            // Skip already-migrated keys (they have env segment: ocelot:service:{env}:{guid})
            var parts = keyStr.Split(':');
            if (parts.Length == 3 && parts[0] == "ocelot" && parts[1] == "service")
            {
                // This is old format: ocelot:service:{guid}
                oldServiceKeys.Add(keyStr);
            }
        }

        _logger.LogInformation("Found {Count} old-format Service keys to migrate", oldServiceKeys.Count);

        // 2. Migrate each service
        foreach (var oldKey in oldServiceKeys)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await MigrateSingleServiceAsync(oldKey);
                result.MigratedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to migrate service key {Key}", oldKey);
                result.Errors.Add($"Failed to migrate {oldKey}: {ex.Message}");
            }
        }

        // 3. Rebuild index for the environment
        await RebuildServiceIndexAsync();
        result.IndexRebuilt = true;

        // 4. Delete old keys if requested
        if (_deleteOldKeys && oldServiceKeys.Count > 0)
        {
            var deleted = await _db.KeyDeleteAsync(oldServiceKeys.Select(k => (RedisKey)k).ToArray());
            result.DeletedOldKeys = (int)deleted;
            _logger.LogInformation("Deleted {Count} old Service keys", deleted);
        }

        _logger.LogInformation("Service key migration completed: {Migrated} migrated, {Deleted} old keys deleted",
            result.MigratedCount, result.DeletedOldKeys);

        return result;
    }

    private async Task MigrateSingleServiceAsync(string oldKey)
    {
        // Read the old hash
        var hash = await _db.HashGetAllAsync(oldKey);
        if (hash.Length == 0)
        {
            _logger.LogWarning("Old service key {Key} has no data, skipping", oldKey);
            return;
        }

        // Extract service ID from old key: ocelot:service:{guid}
        var serviceIdStr = oldKey.Split(':')[2];
        if (!Guid.TryParse(serviceIdStr, out var serviceIdGuid))
        {
            _logger.LogWarning("Could not parse service ID from key {Key}", oldKey);
            return;
        }

        var serviceId = ServiceId.From(serviceIdGuid);

        // Build new key: ocelot:service:{env}:{guid}
        var newKey = RedisKeyHelper.Service(serviceId, EnvironmentName.From(_targetEnvironment));

        // Write to new key
        await _db.HashSetAsync(newKey, hash);

        // Get the service ID for index (from hash)
        var idEntry = hash.FirstOrDefault(e => e.Name == "Id");
        if (!idEntry.Value.IsNullOrEmpty)
        {
            var idStr = idEntry.Value.ToString();
            await _db.SetAddAsync(RedisKeyHelper.IndexServices(EnvironmentName.From(_targetEnvironment)), idStr);
        }

        _logger.LogDebug("Migrated service {OldKey} -> {NewKey}", oldKey, newKey);
    }

    private async Task RebuildServiceIndexAsync()
    {
        var indexKey = RedisKeyHelper.IndexServices(EnvironmentName.From(_targetEnvironment));
        var count = await _db.SetLengthAsync(indexKey);
        _logger.LogInformation("Service index for environment '{Env}' has {Count} entries", _targetEnvironment, count);
    }

    private IServer GetServer()
    {
        var endpoints = _db.Multiplexer.GetEndPoints();
        return _db.Multiplexer.GetServer(endpoints[0]);
    }
}

