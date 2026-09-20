using BitWrite.OcelotControl.Domain.Aggregates.AuditLog;

namespace BitWrite.OcelotControl.Application.Interfaces;

public interface IAuditLogRepository
{
    Task<AuditLog?> GetAsync(string id, CancellationToken cancellationToken = default);
    Task<List<AuditLog>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<List<AuditLog>> GetByActorAsync(string actor, CancellationToken cancellationToken = default);
    Task<List<AuditLog>> GetByActionAsync(string action, CancellationToken cancellationToken = default);
    Task<List<AuditLog>> GetByResourceTypeAsync(string resourceType, CancellationToken cancellationToken = default);
    Task<List<AuditLog>> GetByResourceIdAsync(string resourceId, CancellationToken cancellationToken = default);
    Task<List<AuditLog>> GetByDateRangeAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
    Task<List<AuditLog>> GetFilteredAsync(
        string? actor = null,
        string? action = null,
        string? resourceType = null,
        string? resourceId = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default);
    Task AddAsync(AuditLog auditLog, CancellationToken cancellationToken = default);
    Task<int> GetCountAsync(CancellationToken cancellationToken = default);
    Task<int> GetFilteredCountAsync(
        string? actor = null,
        string? action = null,
        string? resourceType = null,
        string? resourceId = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default);
}
