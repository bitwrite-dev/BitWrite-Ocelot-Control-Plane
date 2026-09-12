using BitWrite.OcelotControl.Api.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace BitWrite.OcelotControl.Api.Controllers;

[ApiController]
[Route("api/v1/runtime")]
public class RuntimeController : BaseApiController
{
    [HttpGet("status")]
    public async Task<ActionResult<RuntimeStatusResponse>> GetRuntimeStatus(
        [FromQuery] string gatewayId)
    {
        try
        {
            // TODO: Implement using UseCase handler
            return NotFound();
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    [HttpGet("gateways")]
    public async Task<ActionResult<RuntimeGatewaysResponse>> GetAllGateways()
    {
        try
        {
            // TODO: Implement using UseCase handler
            var response = new RuntimeGatewaysResponse(new List<RuntimeStatusResponse>());
            return HandleResult(response);
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    [HttpGet("gateways/{id}")]
    public async Task<ActionResult<RuntimeStatusResponse>> GetGatewayRuntime(string id)
    {
        try
        {
            // TODO: Implement using UseCase handler
            return NotFound();
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    [HttpPost("reconcile")]
    public async Task<ActionResult> Reconcile(ReconcileRequest request)
    {
        try
        {
            // TODO: Implement using UseCase handler
            return Ok();
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }
}