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

    public GatewaysController(
        AppGateway.RegisterGatewayCommandHandler registerGatewayCommandHandler,
        AppGateway.GetGatewayQueryHandler getGatewayQueryHandler,
        AppGateway.ListGatewaysQueryHandler listGatewaysQueryHandler,
        AppGateway.UpdateGatewayCommandHandler updateGatewayCommandHandler,
        AppGateway.UpdateGatewayStatusCommandHandler updateGatewayStatusCommandHandler)
    {
        _registerGatewayCommandHandler = registerGatewayCommandHandler;
        _getGatewayQueryHandler = getGatewayQueryHandler;
        _listGatewaysQueryHandler = listGatewaysQueryHandler;
        _updateGatewayCommandHandler = updateGatewayCommandHandler;
        _updateGatewayStatusCommandHandler = updateGatewayStatusCommandHandler;
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