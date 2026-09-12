using BitWrite.OcelotControl.Api.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace BitWrite.OcelotControl.Api.Controllers;

[ApiController]
[Route("api/v1/global-configuration")]
public class GlobalConfigurationController : BaseApiController
{
    [HttpGet]
    public async Task<ActionResult<GlobalConfigurationResponse>> GetGlobalConfiguration()
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

    [HttpPut]
    public async Task<ActionResult<GlobalConfigurationResponse>> UpdateGlobalConfiguration(UpdateGlobalConfigurationRequest request)
    {
        try
        {
            // TODO: Implement using UseCase handler
            var response = new GlobalConfigurationResponse(
                Guid.NewGuid().ToString(),
                request.BaseUrl,
                request.RequestIdKey,
                request.DownstreamScheme,
                request.Timeout,
                request.RateLimit != null ? new RateLimitConfigResponse(request.RateLimit.EnableRateLimiting, request.RateLimit.HttpStatusCode) : null,
                request.QoS != null ? new QoSConfigResponse(request.QoS.TimeoutValue, request.QoS.DurationOfBreak) : null,
                request.HttpHandler != null ? new HttpHandlerConfigResponse(request.HttpHandler.UseProxy, request.HttpHandler.Expect100Continue, request.HttpHandler.MaxConnectionsPerServer) : null,
                request.ServiceDiscovery != null ? new ServiceDiscoveryConfigResponse(
                    request.ServiceDiscovery.Provider,
                    request.ServiceDiscovery.Host,
                    request.ServiceDiscovery.Port,
                    request.ServiceDiscovery.Type,
                    request.ServiceDiscovery.Configuration ?? new()) : null,
                DateTimeOffset.UtcNow
            );
            return HandleResult(response);
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }
}