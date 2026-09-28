using ApiDtos = BitWrite.OcelotControl.Api.DTOs;
using AppGateway = BitWrite.OcelotControl.Application.UseCases.Gateway;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Domain.ValueObjects.Status;
using Microsoft.AspNetCore.Mvc;

namespace BitWrite.OcelotControl.Api.Controllers;

[ApiController]
[Route("api/v1/gateways")]
public class GatewaysController : BaseApiController
{
    private readonly AppGateway.RegisterGatewayCommandHandler _registerGatewayCommandHandler;
    private readonly AppGateway.GetGatewayQueryHandler _getGatewayQueryHandler;
    private readonly AppGateway.ListGatewaysQueryHandler _listGatewaysQueryHandler;
    private readonly AppGateway.UpdateGatewayCommandHandler _updateGatewayCommandHandler;
    private readonly AppGateway.UpdateGatewayStatusCommandHandler _updateGatewayStatusCommandHandler;
    private readonly AppGateway.DeleteGatewayCommandHandler _deleteGatewayCommandHandler;
    private readonly AppGateway.GetGatewayDeletionEligibilityQueryHandler _deletionEligibilityQueryHandler;

    public GatewaysController(
        AppGateway.RegisterGatewayCommandHandler registerGatewayCommandHandler,
        AppGateway.GetGatewayQueryHandler getGatewayQueryHandler,
        AppGateway.ListGatewaysQueryHandler listGatewaysQueryHandler,
        AppGateway.UpdateGatewayCommandHandler updateGatewayCommandHandler,
        AppGateway.UpdateGatewayStatusCommandHandler updateGatewayStatusCommandHandler,
        AppGateway.DeleteGatewayCommandHandler deleteGatewayCommandHandler,
        AppGateway.GetGatewayDeletionEligibilityQueryHandler deletionEligibilityQueryHandler)
    {
        _registerGatewayCommandHandler = registerGatewayCommandHandler;
        _getGatewayQueryHandler = getGatewayQueryHandler;
        _listGatewaysQueryHandler = listGatewaysQueryHandler;
        _updateGatewayCommandHandler = updateGatewayCommandHandler;
        _updateGatewayStatusCommandHandler = updateGatewayStatusCommandHandler;
        _deleteGatewayCommandHandler = deleteGatewayCommandHandler;
        _deletionEligibilityQueryHandler = deletionEligibilityQueryHandler;
    }

    [HttpGet]
    public async Task<ActionResult<ApiDtos.GatewayListResponse>> GetGateways(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            var query = new AppGateway.ListGatewaysQuery(page, pageSize);
            var result = await _listGatewaysQueryHandler.HandleAsync(query);

            var response = new ApiDtos.GatewayListResponse(
                result.Gateways.Select(MapToResponse).ToList(),
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
    public async Task<ActionResult<ApiDtos.GatewayResponse>> GetGateway(string id)
    {
        try
        {
            var query = new AppGateway.GetGatewayQuery(GatewayId.From(Guid.Parse(id)));
            var result = await _getGatewayQueryHandler.HandleAsync(query);

            if (result == null)
                return NotFound();

            return HandleResult(MapToResponse(result));
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    [HttpPost]
    public async Task<ActionResult<ApiDtos.GatewayResponse>> CreateGateway(ApiDtos.CreateGatewayRequest request)
    {
        try
        {
            var command = new AppGateway.RegisterGatewayCommand(
                request.Name,
                request.Description,
                User.Identity?.Name ?? "system"
            );

            var result = await _registerGatewayCommandHandler.HandleAsync(command);
            return HandleResult(MapToResponse(result));
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ApiDtos.GatewayResponse>> UpdateGateway(string id, ApiDtos.UpdateGatewayRequest request)
    {
        try
        {
            var command = new AppGateway.UpdateGatewayCommand(
                GatewayId.From(Guid.Parse(id)),
                request.Name,
                request.Description,
                User.Identity?.Name ?? "system"
            );

            var result = await _updateGatewayCommandHandler.HandleAsync(command);
            return HandleResult(MapToResponse(result));
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    /// <summary>
    /// Removes a gateway that has never been a publication target.
    /// </summary>
    /// <remarks>
    /// Refused once a publication has been addressed to it, since the publication
    /// history is keyed by gateway id. The gateway is not deadlocked by then — it
    /// is simply history, and history is not deletable.
    /// </remarks>
    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteGateway(string id)
    {
        try
        {
            var deleted = await _deleteGatewayCommandHandler.HandleAsync(
                new AppGateway.DeleteGatewayCommand(
                    GatewayId.From(Guid.Parse(id)),
                    User.Identity?.Name ?? "system"));

            return deleted ? NoContent() : NotFound();
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    /// <summary>
    /// Whether this gateway can be deleted, and why not if it cannot.
    /// </summary>
    /// <remarks>
    /// Deletion is refused once a publication has been addressed to the gateway.
    /// Reporting that up front means the operator is told the rule before
    /// confirming a deletion, rather than by the refusal afterwards.
    /// </remarks>
    [HttpGet("{id}/deletion-eligibility")]
    [ProducesResponseType(typeof(AppGateway.GatewayDeletionEligibilityResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppGateway.GatewayDeletionEligibilityResponse>> GetDeletionEligibility(string id)
    {
        try
        {
            return Ok(await _deletionEligibilityQueryHandler.HandleAsync(
                new AppGateway.GetGatewayDeletionEligibilityQuery(
                    GatewayId.From(Guid.Parse(id)),
                    User.Identity?.Name ?? "system")));
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    [HttpPatch("{id}/status")]
    public async Task<ActionResult<ApiDtos.GatewayResponse>> UpdateGatewayStatus(string id, ApiDtos.UpdateGatewayStatusRequest request)
    {
        try
        {
            // Parse RuntimeStatus from string
            var status = RuntimeStatus.From(request.Status);

            var command = new AppGateway.UpdateGatewayStatusCommand(
                GatewayId.From(Guid.Parse(id)),
                status,
                User.Identity?.Name ?? "system"
            );

            var result = await _updateGatewayStatusCommandHandler.HandleAsync(command);
            return HandleResult(MapToResponse(result));
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    private static ApiDtos.GatewayResponse MapToResponse(AppGateway.GatewayResponse gateway)
    {
        return new ApiDtos.GatewayResponse(
            gateway.Id.Value.ToString(),
            gateway.Name,
            gateway.Description,
            gateway.Status.Value,
            gateway.CreatedAt,
            gateway.UpdatedAt
        );
    }
}