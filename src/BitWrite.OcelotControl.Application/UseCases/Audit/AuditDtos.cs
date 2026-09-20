namespace BitWrite.OcelotControl.Application.UseCases.Audit;

public record AuditLogResponse(
    string Id,
    string Actor,
    string Action,
    string ResourceType,
    string ResourceId,
    string Result,
    DateTimeOffset Timestamp
);

public record AuditLogListResponse(
    IReadOnlyList<AuditLogResponse> AuditLogs,
    int TotalCount,
    int Page,
    int PageSize
);

public record AuditStatsResponse(
    int TotalCount,
    int TodayCount,
    int ThisWeekCount,
    int ThisMonthCount
);
