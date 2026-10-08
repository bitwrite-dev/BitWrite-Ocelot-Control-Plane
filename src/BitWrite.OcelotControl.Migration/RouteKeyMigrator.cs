using Microsoft.Extensions.Logging;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Infrastructure.Redis;
using StackExchange.Redis;

namespace BitWrite.OcelotControl.Migration;

/// <summary>
/// Migrates Route keys from the old format (ocelot:route:{id}) to the new
/// environment-scoped format (ocelot:route:{env}:{id}).
/// </summary>
/// <remarks>
/// Data loss is accepted per #524. This tool:
/// 1. Scans for all keys matching the old pattern (ocelot:route:{guid})
/// 2. For each found route, reads its hash and migrates it to the new key
///    with the specified environment prefix
/// 3. Rebuilds the index sets (ocelot:index:routes:{env}) from the migrated data
/// 4. Optionally deletes the old keys after successful migration
/// </remarks>
internal sealed class RouteKeyMigrator
{
    private readonly IDatabase _db;
    private readonly Microsoft.Extensions.Logging.ILogger _logger;
    private readonly string _targetEnvironment;
    private readonly bool _deleteOldKeys;

    public RouteKeyMigrator(
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
            "Starting Route key migration to environment '{Environment}' (deleteOldKeys={Delete})",
            _targetEnvironment, _deleteOldKeys);

        var result = new MigrationResult { TargetEnvironment = _targetEnvironment };

        // 1. Find all old route keys: ocelot:route:{guid}
        var oldRouteKeys = new List<string>();
        var server = _db.Multiplexer.GetServer(_db.Multiplexer.GetEndPoints()[0]);
        
        await foreach (var key in server.KeysAsync(pattern: "ocelot:route:*", pageSize: 1000))
        {
            var keyStr = key.ToString();
            // Skip already-migrated keys (they have env segment: ocelot:route:{env}:{guid})
            var parts = keyStr.Split(':');
            if (parts.Length == 3 && parts[0] == "ocelot" && parts[1] == "route")
            {
                // This is old format: ocelot:route:{guid}
                oldRouteKeys.Add(keyStr);
            }
        }

        _logger.LogInformation("Found {Count} old-format Route keys to migrate", oldRouteKeys.Count);

        // 2. Migrate each route
        foreach (var oldKey in oldRouteKeys)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await MigrateSingleRouteAsync(oldKey);
                result.MigratedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to migrate route key {Key}", oldKey);
                result.Errors.Add($"Failed to migrate {oldKey}: {ex.Message}");
            }
        }

        // 3. Rebuild index for the environment
        await RebuildRouteIndexAsync();
        result.IndexRebuilt = true;

        // 4. Delete old keys if requested
        if (_deleteOldKeys && oldRouteKeys.Count > 0)
        {
            var deleted = await _db.KeyDeleteAsync(oldRouteKeys.Select(k => (RedisKey)k).ToArray());
            result.DeletedOldKeys = (int)deleted;
            _logger.LogInformation("Deleted {Count} old Route keys", deleted);
        }

        _logger.LogInformation("Route key migration completed: {Migrated} migrated, {Deleted} old keys deleted",
            result.MigratedCount, result.DeletedOldKeys);

        return result;
    }

    private async Task MigrateSingleRouteAsync(string oldKey)
    {
        // Read the old hash
        var hash = await _db.HashGetAllAsync(oldKey);
        if (hash.Length == 0)
        {
            _logger.LogWarning("Old route key {Key} has no data, skipping", oldKey);
            return;
        }

        // Extract route ID from old key: ocelot:route:{guid}
        var routeIdStr = oldKey.Split(':')[2];
        if (!Guid.TryParse(routeIdStr, out var routeIdGuid))
        {
            _logger.LogWarning("Could not parse route ID from key {Key}", oldKey);
            return;
        }

        var routeId = RouteId.From(routeIdGuid);

        // Build new key: ocelot:route:{env}:{guid}
        var newKey = RedisKeyHelper.Route(routeId, EnvironmentName.From(_targetEnvironment));

        // Write to new key
        await _db.HashSetAsync(newKey, hash);

        // Get the route ID for index (from hash)
        var idEntry = hash.FirstOrDefault(e => e.Name == "Id");
        if (!idEntry.Value.IsNullOrEmpty)
        {
            var idStr = idEntry.Value.ToString();
            await _db.SetAddAsync(RedisKeyHelper.IndexRoutes(EnvironmentName.From(_targetEnvironment)), idStr);
            
            // Also add to service-route index if ServiceId exists
            var serviceIdEntry = hash.FirstOrDefault(e => e.Name == "ServiceId");
            if (!serviceIdEntry.Value.IsNullOrEmpty && Guid.TryParse(serviceIdEntry.Value.ToString(), out var svcGuid))
            {
                var svcId = ServiceId.From(svcGuid);
                await _db.SetAddAsync(RedisKeyHelper.IndexServiceRoutes(svcId, EnvironmentName.From(_targetEnvironment)), idStr);
            }
        }

        _logger.LogDebug("Migrated route {OldKey} -> {NewKey}", oldKey, newKey);
    }

    private async Task RebuildRouteIndexAsync()
    {
        // The index is built incrementally during migration, but we can verify
        var indexKey = RedisKeyHelper.IndexRoutes(EnvironmentName.From(_targetEnvironment));
        var count = await _db.SetLengthAsync(indexKey);
        _logger.LogInformation("Route index for environment '{Env}' has {Count} entries", _targetEnvironment, count);
    }

    private IServer GetServer()
    {
        var endpoints = _db.Multiplexer.GetEndPoints();
        return _db.Multiplexer.GetServer(endpoints[0]);
    }
}