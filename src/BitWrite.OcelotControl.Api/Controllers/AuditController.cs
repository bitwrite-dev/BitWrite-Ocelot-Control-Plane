using BitWrite.OcelotControl.Api.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace BitWrite.OcelotControl.Api.Controllers;

[ApiController]
[Route("api/v1/audit")]
public class AuditController : BaseApiController
{
    [HttpGet]
    public async Task<ActionResult<AuditListResponse>> GetAuditLogs(
        [FromQuery] string? actor = null,
        [FromQuery] string? action = null,
        [FromQuery] string? resourceType = null,
        [FromQuery] string? resourceId = null,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            // TODO: Implement using UseCase handler
            var response = new AuditListResponse(
                new List<AuditResponse>(),
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