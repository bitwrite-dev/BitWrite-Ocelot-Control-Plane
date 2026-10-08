using System.ComponentModel.DataAnnotations;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using Microsoft.AspNetCore.Mvc;
using ApiDtos = BitWrite.OcelotControl.Api.DTOs;
using AppRuntime = BitWrite.OcelotControl.Application.UseCases.Runtime;

namespace BitWrite.OcelotControl.Api.Controllers;

[ApiController]
[Route("api/v1/runtime")]
public class RuntimeController : BaseApiController
{
    private readonly AppRuntime.GetRuntimeStatusQueryHandler _getRuntimeStatusQueryHandler;
    private readonly AppRuntime.GetAllGatewaysQueryHandler _getAllGatewaysQueryHandler;
    private readonly AppRuntime.GetGatewayRuntimeDetailQueryHandler _getGatewayRuntimeDetailQueryHandler;
    private readonly AppRuntime.ReconcileGatewayCommandHandler _reconcileGatewayCommandHandler;
    private readonly AppRuntime.GetDeliveryMetricsQueryHandler _getDeliveryMetricsQueryHandler;

    public RuntimeController(
        AppRuntime.GetRuntimeStatusQueryHandler getRuntimeStatusQueryHandler,
        AppRuntime.GetAllGatewaysQueryHandler getAllGatewaysQueryHandler,
        AppRuntime.GetGatewayRuntimeDetailQueryHandler getGatewayRuntimeDetailQueryHandler,
        AppRuntime.ReconcileGatewayCommandHandler reconcileGatewayCommandHandler,
        AppRuntime.GetDeliveryMetricsQueryHandler getDeliveryMetricsQueryHandler)
    {
        _getRuntimeStatusQueryHandler = getRuntimeStatusQueryHandler;
        _getAllGatewaysQueryHandler = getAllGatewaysQueryHandler;
        _getGatewayRuntimeDetailQueryHandler = getGatewayRuntimeDetailQueryHandler;
        _reconcileGatewayCommandHandler = reconcileGatewayCommandHandler;
        _getDeliveryMetricsQueryHandler = getDeliveryMetricsQueryHandler;
    }

    /// <summary>
    /// How configuration delivery to gateways has been going.
    /// </summary>
    /// <remarks>
    /// Configuration delivery, not request traffic. No request rate, latency or error
    /// rate appears here because none is recorded anywhere in the system; reporting a
    /// zero for them would read as a measurement. What it does report is every attempt
    /// a gateway made to apply a snapshot, and what happened.
    /// </remarks>
    /// <param name="from">Inclusive start of the range. Defaults to 24 hours ago.</param>
    /// <param name="to">Exclusive end of the range. Defaults to now.</param>
    /// <param name="limit">Most recent attempts to consider, newest first.</param>
    [HttpGet("delivery-metrics")]
    public async Task<ActionResult<ApiDtos.DeliveryMetricsResponse>> GetDeliveryMetrics(
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        [FromQuery] int limit = 1000)
    {
        try
        {
            var query = new AppRuntime.GetDeliveryMetricsQuery(from, to, limit);
            var result = await _getDeliveryMetricsQueryHandler.HandleAsync(query);

            return HandleResult(new ApiDtos.DeliveryMetricsResponse(
                result.From,
                result.To,
                result.ConsideredAttempts,
                result.TotalAttempts,
                result.Successful,
                result.Failed,
                result.SuccessRate,
                result.SnapshotVersions.ToList(),
                result.Gateways.Select(gateway => new ApiDtos.GatewayDeliveryMetrics(
                    gateway.GatewayId,
                    gateway.Attempts,
                    gateway.Successful,
                    gateway.Failed,
                    gateway.SuccessRate,
                    gateway.LastAttemptAt,
                    gateway.RecentErrors.ToList())).ToList(),
                result.Errors.Select(error => new ApiDtos.DeliveryErrorFrequency(
                    error.Message,
                    error.Occurrences,
                    error.LastSeenAt)).ToList()));
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    [HttpGet("status")]
    public async Task<ActionResult<ApiDtos.RuntimeStatusResponse>> GetRuntimeStatus(
        [FromQuery] string gatewayId)
    {
        try
        {
            var query = new AppRuntime.GetRuntimeStatusQuery(GatewayId.From(gatewayId));
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
            var query = new AppRuntime.GetGatewayRuntimeDetailQuery(GatewayId.From(id));
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
                GatewayId.From(request.GatewayId),
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
