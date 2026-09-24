using ApiDtos = BitWrite.OcelotControl.Api.DTOs;
using AppPublication = BitWrite.OcelotControl.Application.UseCases.Publication;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BitWrite.OcelotControl.Api.Controllers;

[ApiController]
[Route("api/v1/publications")]
public class PublicationsController : BaseApiController
{
    private readonly AppPublication.ListPublicationsQueryHandler _listPublicationsQueryHandler;
    private readonly AppPublication.GetCurrentPublicationQueryHandler _getCurrentPublicationQueryHandler;
    private readonly AppPublication.GetPublicationHistoryQueryHandler _getPublicationHistoryQueryHandler;

    public PublicationsController(
        AppPublication.ListPublicationsQueryHandler listPublicationsQueryHandler,
        AppPublication.GetCurrentPublicationQueryHandler getCurrentPublicationQueryHandler,
        AppPublication.GetPublicationHistoryQueryHandler getPublicationHistoryQueryHandler)
    {
        _listPublicationsQueryHandler = listPublicationsQueryHandler;
        _getCurrentPublicationQueryHandler = getCurrentPublicationQueryHandler;
        _getPublicationHistoryQueryHandler = getPublicationHistoryQueryHandler;
    }

    [HttpGet]
    public async Task<ActionResult<ApiDtos.PublicationListResponse>> GetPublications(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            var query = new AppPublication.ListPublicationsQuery(page, pageSize);
            var result = await _listPublicationsQueryHandler.HandleAsync(query);

            var response = new ApiDtos.PublicationListResponse(
                result.Publications.Select(MapToResponse).ToList(),
                result.TotalCount,
                result.Page,
                result.PageSize
            );

            return HandleResult(response);
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    [HttpGet("current")]
    public async Task<ActionResult<ApiDtos.CurrentPublicationResponse>> GetCurrentPublication()
    {
        try
        {
            var query = new AppPublication.GetCurrentPublicationQuery();
            var result = await _getCurrentPublicationQueryHandler.HandleAsync(query);

            var response = new ApiDtos.CurrentPublicationResponse(
                result.Current != null ? MapToResponse(result.Current) : null,
                result.History.Select(MapToResponse).ToList()
            );

            return HandleResult(response);
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    [HttpGet("history")]
    public async Task<ActionResult<ApiDtos.PublicationListResponse>> GetPublicationHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            var query = new AppPublication.GetPublicationHistoryQuery(page, pageSize);
            var result = await _getPublicationHistoryQueryHandler.HandleAsync(query);

            var response = new ApiDtos.PublicationListResponse(
                result.Publications.Select(MapToResponse).ToList(),
                result.TotalCount,
                result.Page,
                result.PageSize
            );

            return HandleResult(response);
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    private static ApiDtos.PublicationResponse MapToResponse(AppPublication.PublicationResponse publication)
    {
        return new ApiDtos.PublicationResponse(
            publication.Id.Value.ToString(),
            publication.SnapshotVersion.Value,
            publication.Status.Value,
            publication.InitiatedBy,
            publication.StartedAt,
            publication.CompletedAt,
            publication.FailureReason,
            publication.GatewayStates.Select(MapToGatewayState).ToList()
        );
    }

    private static ApiDtos.GatewayDeploymentStateResponse MapToGatewayState(AppPublication.GatewayDeploymentStateResponse state)
    {
        return new ApiDtos.GatewayDeploymentStateResponse(
            state.GatewayId.Value.ToString(),
            state.Status,
            state.ReceivedAt,
            state.ValidatedAt,
            state.AppliedAt,
            state.HealthyAt,
            state.IsValid,
            state.FailedAt,
            state.FailureReason
        );
    }
}