namespace BitWrite.OcelotControl.Application.UseCases.Audit;

public record GetAuditLogsQuery(
    string? Actor = null,
    string? Action = null,
    string? ResourceType = null,
    string? ResourceId = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int Page = 1,
    int PageSize = 20
);

public record GetAuditLogByIdQuery(
    string Id
);

public record GetAuditStatsQuery();
