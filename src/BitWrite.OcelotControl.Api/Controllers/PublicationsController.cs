using BitWrite.OcelotControl.Api.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace BitWrite.OcelotControl.Api.Controllers;

[ApiController]
[Route("api/v1/publications")]
public class PublicationsController : BaseApiController
{
    [HttpGet]
    public async Task<ActionResult<PublicationListResponse>> GetPublications(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            // TODO: Implement using UseCase handler
            var response = new PublicationListResponse(
                new List<PublicationResponse>(),
                0,
                page,
                pageSize
            );
            return HandleResult(response);
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    [HttpGet("current")]
    public async Task<ActionResult<CurrentPublicationResponse>> GetCurrentPublication()
    {
        try
        {
            // TODO: Implement using UseCase handler
            var response = new CurrentPublicationResponse(
                null,
                new List<PublicationResponse>()
            );
            return HandleResult(response);
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    [HttpGet("history")]
    public async Task<ActionResult<PublicationListResponse>> GetPublicationHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            // TODO: Implement using UseCase handler
            var response = new PublicationListResponse(
                new List<PublicationResponse>(),
                0,
                page,
                pageSize
            );
            return HandleResult(response);
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }
}