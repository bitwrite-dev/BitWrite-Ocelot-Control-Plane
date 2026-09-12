namespace BitWrite.OcelotControl.Api.DTOs;

public record ErrorResponse(
    string CorrelationId,
    string Error,
    string Type,
    object? Details = null
);

public record ValidationErrorResponse(
    string CorrelationId,
    string Error,
    string Type,
    Dictionary<string, string[]> Details
);