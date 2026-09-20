using BitWrite.OcelotControl.Api.DTOs;
using BitWrite.OcelotControl.Application.UseCases.Audit;
using Microsoft.AspNetCore.Mvc;

namespace BitWrite.OcelotControl.Api.Controllers;

[ApiController]
[Route("api/v1/audit")]
public class AuditController : BaseApiController
{
    private readonly GetAuditLogsQueryHandler _getAuditLogsQueryHandler;
    private readonly GetAuditLogByIdQueryHandler _getAuditLogByIdQueryHandler;
    private readonly GetAuditStatsQueryHandler _getAuditStatsQueryHandler;

    public AuditController(
        GetAuditLogsQueryHandler getAuditLogsQueryHandler,
        GetAuditLogByIdQueryHandler getAuditLogByIdQueryHandler,
        GetAuditStatsQueryHandler getAuditStatsQueryHandler)
    {
        _getAuditLogsQueryHandler = getAuditLogsQueryHandler;
        _getAuditLogByIdQueryHandler = getAuditLogByIdQueryHandler;
        _getAuditStatsQueryHandler = getAuditStatsQueryHandler;
    }

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
            var query = new GetAuditLogsQuery(actor, action, resourceType, resourceId, from, to, page, pageSize);
            var result = await _getAuditLogsQueryHandler.HandleAsync(query);

            var response = new AuditListResponse(
                result.AuditLogs.Select(a => new AuditResponse(
                    a.Id, a.Actor, a.Action, a.ResourceType, a.ResourceId, a.Result, a.Timestamp
                )).ToList(),
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

    [HttpGet("{id}")]
    public async Task<ActionResult<AuditResponse>> GetAuditLogById(string id)
    {
        try
        {
            var query = new GetAuditLogByIdQuery(id);
            var result = await _getAuditLogByIdQueryHandler.HandleAsync(query);

            if (result == null)
                return NotFound();

            var response = new AuditResponse(
                result.Id, result.Actor, result.Action, result.ResourceType,
                result.ResourceId, result.Result, result.Timestamp
            );

            return HandleResult(response);
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    [HttpGet("stats")]
    public async Task<ActionResult<object>> GetAuditStats()
    {
        try
        {
            var query = new GetAuditStatsQuery();
            var result = await _getAuditStatsQueryHandler.HandleAsync(query);

            return HandleResult(result);
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }
}
