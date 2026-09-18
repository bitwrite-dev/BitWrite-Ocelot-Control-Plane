namespace BitWrite.OcelotControl.Application.UseCases.Gateway;

public record ListGatewaysQuery(
    int Page = 1,
    int PageSize = 20
);