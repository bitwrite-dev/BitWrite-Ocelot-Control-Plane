using ApiDtos = BitWrite.OcelotControl.Api.DTOs;
using AppRuntime = BitWrite.OcelotControl.Application.UseCases.Runtime;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace BitWrite.OcelotControl.Api.Controllers;

[ApiController]
[Route("api/v1/runtime")]
public class RuntimeController : BaseApiController
{
    private readonly AppRuntime.GetRuntimeStatusQueryHandler _getRuntimeStatusQueryHandler;
    private readonly AppRuntime.GetAllGatewaysQueryHandler _getAllGatewaysQueryHandler;
    private readonly AppRuntime.GetGatewayRuntimeDetailQueryHandler _getGatewayRuntimeDetailQueryHandler;
    private readonly AppRuntime.ReconcileGatewayCommandHandler _reconcileGatewayCommandHandler;

    public RuntimeController(
        AppRuntime.GetRuntimeStatusQueryHandler getRuntimeStatusQueryHandler,
        AppRuntime.GetAllGatewaysQueryHandler getAllGatewaysQueryHandler,
        AppRuntime.GetGatewayRuntimeDetailQueryHandler getGatewayRuntimeDetailQueryHandler,
        AppRuntime.ReconcileGatewayCommandHandler reconcileGatewayCommandHandler)
    {
        _getRuntimeStatusQueryHandler = getRuntimeStatusQueryHandler;
        _getAllGatewaysQueryHandler = getAllGatewaysQueryHandler;
        _getGatewayRuntimeDetailQueryHandler = getGatewayRuntimeDetailQueryHandler;
        _reconcileGatewayCommandHandler = reconcileGatewayCommandHandler;
    }

    [HttpGet("status")]
    public async Task<ActionResult<ApiDtos.RuntimeStatusResponse>> GetRuntimeStatus(
        [FromQuery] string gatewayId)
    {
        try
        {
            var query = new AppRuntime.GetRuntimeStatusQuery(GatewayId.From(Guid.Parse(gatewayId)));
            var result = await _getRuntimeStatusQueryHandler.HandleAsync(query);

            if (result == null)
                return NotFound();

            return HandleResult(MapToResponse(result));
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    [HttpGet("gateways")]
    public async Task<ActionResult<ApiDtos.RuntimeGatewaysResponse>> GetAllGateways()
    {
        try
        {
            var query = new AppRuntime.GetAllGatewaysQuery();
            var result = await _getAllGatewaysQueryHandler.HandleAsync(query);

            var response = new ApiDtos.RuntimeGatewaysResponse(
                result.Gateways.Select(MapToResponse).ToList()
            );

            return HandleResult(response);
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    [HttpGet("gateways/{id}")]
    public async Task<ActionResult<ApiDtos.RuntimeStatusResponse>> GetGatewayRuntime(string id)
    {
        try
        {
            var query = new AppRuntime.GetGatewayRuntimeDetailQuery(GatewayId.From(Guid.Parse(id)));
            var result = await _getGatewayRuntimeDetailQueryHandler.HandleAsync(query);

            if (result == null)
                return NotFound();

            return HandleResult(MapToResponse(result));
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    [HttpPost("reconcile")]
    public async Task<ActionResult<ApiDtos.ReconcileResponse>> Reconcile(ApiDtos.ReconcileRequest request)
    {
        try
        {
            var command = new AppRuntime.ReconcileGatewayCommand(
                GatewayId.From(Guid.Parse(request.GatewayId)),
                SnapshotVersion.From(request.TargetVersion),
                request.InitiatedBy
            );

            var result = await _reconcileGatewayCommandHandler.HandleAsync(command);

            if (!result.Success)
                return BadRequest(new { error = result.ErrorMessage });

            var response = new ApiDtos.ReconcileResponse(
                result.GatewayId.Value.ToString(),
                result.TargetVersion.Value,
                result.Success,
                result.ErrorMessage,
                result.ReconciledAt
            );

            return HandleResult(response);
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    private static ApiDtos.RuntimeStatusResponse MapToResponse(AppRuntime.RuntimeStatusResponse runtime)
    {
        return new ApiDtos.RuntimeStatusResponse(
            runtime.GatewayId.Value.ToString(),
            runtime.Status.Value,
            runtime.CurrentVersion,
            runtime.TargetVersion,
            runtime.LastHeartbeat,
            runtime.LastSynchronized,
            runtime.LastConfigApplied,
            runtime.RuntimeInfo.ToDictionary(k => k.Key, v => v.Value),
            runtime.Capabilities.ToList(),
            runtime.ActiveRoutes.ToList()
        );
    }

    private static ApiDtos.RuntimeStatusResponse MapToResponse(AppRuntime.GatewayRuntimeDetailResponse runtime)
    {
        return new ApiDtos.RuntimeStatusResponse(
            runtime.GatewayId.Value.ToString(),
            runtime.Status.Value,
            runtime.CurrentVersion,
            runtime.TargetVersion,
            runtime.LastHeartbeat,
            runtime.LastSynchronized,
            runtime.LastConfigApplied,
            runtime.RuntimeInfo.ToDictionary(k => k.Key, v => v.Value),
            runtime.Capabilities.ToList(),
            runtime.ActiveRoutes.ToList()
        );
    }
}

public record ReconcileResponse(
    string GatewayId,
    int TargetVersion,
    bool Success,
    string? ErrorMessage,
    DateTimeOffset ReconciledAt
);