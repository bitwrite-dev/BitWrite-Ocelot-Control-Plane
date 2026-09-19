using BitWrite.OcelotControl.Application.UseCases.Publication;

public record ListPublicationsQuery(
    int Page = 1,
    int PageSize = 20
);

public record GetCurrentPublicationQuery();

public record GetPublicationHistoryQuery(
    int Page = 1,
    int PageSize = 20
);