using BitWrite.OcelotControl.Api.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace BitWrite.OcelotControl.Api.Controllers;

[ApiController]
[Route("api/v1/gateways")]
public class GatewaysController : BaseApiController
{
    [HttpGet]
    public async Task<ActionResult<GatewayListResponse>> GetGateways(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            // TODO: Implement using UseCase handler
            var response = new GatewayListResponse(
                new List<GatewayResponse>(),
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

    [HttpGet("{id}")]
    public async Task<ActionResult<GatewayResponse>> GetGateway(string id)
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

    [HttpPost]
    public async Task<ActionResult<GatewayResponse>> CreateGateway(CreateGatewayRequest request)
    {
        try
        {
            // TODO: Implement using UseCase handler
            var response = new GatewayResponse(
                Guid.NewGuid().ToString(),
                request.Name,
                request.Description,
                "Active",
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow
            );
            return HandleResult(response);
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<GatewayResponse>> UpdateGateway(string id, UpdateGatewayRequest request)
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

    [HttpPatch("{id}/status")]
    public async Task<ActionResult<GatewayResponse>> UpdateGatewayStatus(string id, UpdateGatewayStatusRequest request)
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
}