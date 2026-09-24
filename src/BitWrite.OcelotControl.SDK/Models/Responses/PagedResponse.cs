namespace BitWrite.OcelotControl.SDK.Models.Responses;

public record PagedResponse<T>(
    List<T> Items,
    int TotalCount,
    int Page,
    int PageSize
);