using System.ComponentModel.DataAnnotations;

namespace BitWrite.OcelotControl.Api.DTOs;

public record AuditFilterRequest(
    string? Actor,
    string? Action,
    string? ResourceType,
    string? ResourceId,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int Page = 1,
    int PageSize = 20
);

public record AuditResponse(
    string Id,
    string Actor,
    string Action,
    string ResourceType,
    string ResourceId,
    string Result,
    DateTimeOffset Timestamp
);

public record AuditListResponse(
    List<AuditResponse> Audits,
    int TotalCount,
    int Page,
    int PageSize
);