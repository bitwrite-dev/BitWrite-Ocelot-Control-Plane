using BitWrite.OcelotControl.Application.Interfaces;

namespace BitWrite.OcelotControl.Application.UseCases.Audit;

public class GetAuditLogByIdQueryHandler
{
    private readonly IAuditLogRepository _auditLogRepository;

    public GetAuditLogByIdQueryHandler(IAuditLogRepository auditLogRepository)
    {
        _auditLogRepository = auditLogRepository;
    }

    public async Task<AuditLogResponse?> HandleAsync(GetAuditLogByIdQuery query, CancellationToken cancellationToken = default)
    {
        var auditLog = await _auditLogRepository.GetAsync(query.Id, cancellationToken);

        if (auditLog == null)
            return null;

        return MapToResponse(auditLog);
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
