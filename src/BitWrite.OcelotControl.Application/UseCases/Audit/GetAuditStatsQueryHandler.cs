using BitWrite.OcelotControl.Application.Interfaces;

namespace BitWrite.OcelotControl.Application.UseCases.Audit;

public class GetAuditStatsQueryHandler
{
    private readonly IAuditLogRepository _auditLogRepository;

    public GetAuditStatsQueryHandler(IAuditLogRepository auditLogRepository)
    {
        _auditLogRepository = auditLogRepository;
    }

    public async Task<AuditStatsResponse> HandleAsync(GetAuditStatsQuery query, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var today = now.Date;
        var weekStart = today.AddDays(-(int)today.DayOfWeek);
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var totalCount = await _auditLogRepository.GetCountAsync(cancellationToken);
        var todayCount = await _auditLogRepository.GetFilteredCountAsync(from: today, cancellationToken: cancellationToken);
        var weekCount = await _auditLogRepository.GetFilteredCountAsync(from: weekStart, cancellationToken: cancellationToken);
        var monthCount = await _auditLogRepository.GetFilteredCountAsync(from: monthStart, cancellationToken: cancellationToken);

        return new AuditStatsResponse(totalCount, todayCount, weekCount, monthCount);
    }
}
