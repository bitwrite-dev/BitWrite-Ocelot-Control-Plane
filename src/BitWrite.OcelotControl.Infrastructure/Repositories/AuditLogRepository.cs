using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Domain.Aggregates.AuditLog;
using BitWrite.OcelotControl.Infrastructure.Redis;
using StackExchange.Redis;
using System.Text.Json;

namespace BitWrite.OcelotControl.Infrastructure.Repositories;

public class RedisAuditLogRepository : RedisRepositoryBase, IAuditLogRepository
{
    private const string AuditLogsIndexKey = "ocelot:index:audit-logs";

    public RedisAuditLogRepository(IConnectionMultiplexer connectionMultiplexer)
        : base(connectionMultiplexer)
    {
    }

    public async Task<AuditLog?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        var key = RedisKeyHelper.AuditLog(id);
        var entries = await GetHashAsync(key);

        if (entries.Length == 0)
            return null;

        return DeserializeAuditLog(entries);
    }

    public async Task<List<AuditLog>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var auditLogIds = await Database.SortedSetRangeByScoreAsync(
            AuditLogsIndexKey,
            double.NegativeInfinity,
            double.PositiveInfinity,
            Exclude.None,
            Order.Descending);

        var auditLogs = new List<AuditLog>();

        foreach (var id in auditLogIds)
        {
            try
            {
                var auditLog = await GetAsync(id.ToString(), cancellationToken);
                if (auditLog != null)
                    auditLogs.Add(auditLog);
            }
            catch
            {
                // Skip invalid IDs
            }
        }

        return auditLogs;
    }

    public async Task<List<AuditLog>> GetByActorAsync(string actor, CancellationToken cancellationToken = default)
    {
        var allLogs = await GetAllAsync(cancellationToken);
        return allLogs.Where(l => l.Actor.Equals(actor, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public async Task<List<AuditLog>> GetByActionAsync(string action, CancellationToken cancellationToken = default)
    {
        var allLogs = await GetAllAsync(cancellationToken);
        return allLogs.Where(l => l.Action.Equals(action, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public async Task<List<AuditLog>> GetByResourceTypeAsync(string resourceType, CancellationToken cancellationToken = default)
    {
        var allLogs = await GetAllAsync(cancellationToken);
        return allLogs.Where(l => l.ResourceType.Equals(resourceType, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public async Task<List<AuditLog>> GetByResourceIdAsync(string resourceId, CancellationToken cancellationToken = default)
    {
        var allLogs = await GetAllAsync(cancellationToken);
        return allLogs.Where(l => l.ResourceId.Equals(resourceId, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public async Task<List<AuditLog>> GetByDateRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
    {
        var allLogs = await GetAllAsync(cancellationToken);
        return allLogs.Where(l => l.Timestamp >= from && l.Timestamp <= to).ToList();
    }

    public async Task<List<AuditLog>> GetFilteredAsync(
        string? actor = null,
        string? action = null,
        string? resourceType = null,
        string? resourceId = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default)
    {
        var allLogs = await GetAllAsync(cancellationToken);
        var query = allLogs.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(actor))
            query = query.Where(l => l.Actor.Contains(actor, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(l => l.Action.Contains(action, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(resourceType))
            query = query.Where(l => l.ResourceType.Contains(resourceType, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(resourceId))
            query = query.Where(l => l.ResourceId.Contains(resourceId, StringComparison.OrdinalIgnoreCase));

        if (from.HasValue)
            query = query.Where(l => l.Timestamp >= from.Value);

        if (to.HasValue)
            query = query.Where(l => l.Timestamp <= to.Value);

        return query.OrderByDescending(l => l.Timestamp).ToList();
    }

    public async Task AddAsync(AuditLog auditLog, CancellationToken cancellationToken = default)
    {
        var key = RedisKeyHelper.AuditLog(auditLog.Id);
        var entries = SerializeAuditLog(auditLog);

        var transaction = Database.CreateTransaction();
        transaction.HashSetAsync(key, entries);
        transaction.SortedSetAddAsync(AuditLogsIndexKey, auditLog.Id, ToUnixTimestamp(auditLog.Timestamp));
        await transaction.ExecuteAsync();
    }

    public async Task<int> GetCountAsync(CancellationToken cancellationToken = default)
    {
        var count = await Database.SortedSetLengthAsync(AuditLogsIndexKey);
        return (int)count;
    }

    public async Task<int> GetFilteredCountAsync(
        string? actor = null,
        string? action = null,
        string? resourceType = null,
        string? resourceId = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default)
    {
        var filtered = await GetFilteredAsync(actor, action, resourceType, resourceId, from, to, cancellationToken);
        return filtered.Count;
    }

    private HashEntry[] SerializeAuditLog(AuditLog auditLog)
    {
        return new HashEntry[]
        {
            new("Id", auditLog.Id),
            new("Actor", auditLog.Actor),
            new("Action", auditLog.Action),
            new("ResourceType", auditLog.ResourceType),
            new("ResourceId", auditLog.ResourceId),
            new("Result", auditLog.Result),
            new("Timestamp", auditLog.Timestamp.ToString("O")),
            new("CorrelationId", auditLog.CorrelationId ?? ""),
            new("Details", auditLog.Details ?? "")
        };
    }

    private AuditLog DeserializeAuditLog(HashEntry[] entries)
    {
        // Reconstitute, not Create: Create generates a new id and stamps the
        // current time, so every read rewrote the identity and the recorded
        // timestamp of an audit entry.
        return AuditLog.Reconstitute(
            GetEntry(entries, "Id"),
            GetEntry(entries, "Actor"),
            GetEntry(entries, "Action"),
            GetEntry(entries, "ResourceType"),
            GetEntry(entries, "ResourceId"),
            GetEntry(entries, "Result"),
            DateTimeOffset.Parse(GetEntry(entries, "Timestamp")),
            NullIfEmpty(GetEntry(entries, "CorrelationId")),
            NullIfEmpty(GetEntry(entries, "Details")));
    }

    private static string? NullIfEmpty(string value) =>
        string.IsNullOrEmpty(value) ? null : value;

    private static double ToUnixTimestamp(DateTimeOffset dateTime)
    {
        return dateTime.ToUnixTimeSeconds();
    }
}
