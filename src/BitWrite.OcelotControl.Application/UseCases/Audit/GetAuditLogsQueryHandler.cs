using BitWrite.OcelotControl.Application.Interfaces;

namespace BitWrite.OcelotControl.Application.UseCases.Audit;

public class GetAuditLogsQueryHandler
{
    private readonly IAuditLogRepository _auditLogRepository;

    public GetAuditLogsQueryHandler(IAuditLogRepository auditLogRepository)
    {
        _auditLogRepository = auditLogRepository;
    }

    public async Task<AuditLogListResponse> HandleAsync(GetAuditLogsQuery query, CancellationToken cancellationToken = default)
    {
        var logs = await _auditLogRepository.GetFilteredAsync(
            query.Actor,
            query.Action,
            query.ResourceType,
            query.ResourceId,
            query.From,
            query.To,
            cancellationToken);

        var totalCount = await _auditLogRepository.GetFilteredCountAsync(
            query.Actor,
            query.Action,
            query.ResourceType,
            query.ResourceId,
            query.From,
            query.To,
            cancellationToken);

        var pagedLogs = logs
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(MapToResponse)
            .ToList();

        return new AuditLogListResponse(pagedLogs, totalCount, query.Page, query.PageSize);
    }

    private static AuditLogResponse MapToResponse(Domain.Aggregates.AuditLog.AuditLog auditLog)
    {
        return new AuditLogResponse(
            auditLog.Id,
            auditLog.Actor,
            auditLog.Action,
            auditLog.ResourceType,
            auditLog.ResourceId,
            auditLog.Result,
            auditLog.Timestamp);
    }
}
