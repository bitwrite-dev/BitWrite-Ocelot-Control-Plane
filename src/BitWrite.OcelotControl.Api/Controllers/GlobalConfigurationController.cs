using ApiDtos = BitWrite.OcelotControl.Api.DTOs;
using AppGlobalConfig = BitWrite.OcelotControl.Application.UseCases.GlobalConfiguration;
using DomainRateLimitConfig = BitWrite.OcelotControl.Domain.Aggregates.GlobalConfiguration.RateLimitConfig;
using DomainQoSConfig = BitWrite.OcelotControl.Domain.Aggregates.GlobalConfiguration.QoSConfig;
using DomainHttpHandlerConfig = BitWrite.OcelotControl.Domain.Aggregates.GlobalConfiguration.HttpHandlerConfig;
using DomainServiceDiscoveryConfig = BitWrite.OcelotControl.Domain.Aggregates.GlobalConfiguration.ServiceDiscoveryConfig;
using Microsoft.AspNetCore.Mvc;

namespace BitWrite.OcelotControl.Api.Controllers;

[ApiController]
[Route("api/v1/global-configuration")]
public class GlobalConfigurationController : BaseApiController
{
    private readonly AppGlobalConfig.GetGlobalConfigurationQueryHandler _getGlobalConfigurationQueryHandler;
    private readonly AppGlobalConfig.UpdateGlobalConfigurationCommandHandler _updateGlobalConfigurationCommandHandler;

    public GlobalConfigurationController(
        AppGlobalConfig.GetGlobalConfigurationQueryHandler getGlobalConfigurationQueryHandler,
        AppGlobalConfig.UpdateGlobalConfigurationCommandHandler updateGlobalConfigurationCommandHandler)
    {
        _getGlobalConfigurationQueryHandler = getGlobalConfigurationQueryHandler;
        _updateGlobalConfigurationCommandHandler = updateGlobalConfigurationCommandHandler;
    }

    [HttpGet]
    public async Task<ActionResult<ApiDtos.GlobalConfigurationResponse>> GetGlobalConfiguration()
    {
        try
        {
            var query = new AppGlobalConfig.GetGlobalConfigurationQuery();
            var result = await _getGlobalConfigurationQueryHandler.HandleAsync(query);

            if (result == null)
                return NotFound();

            return HandleResult(MapToResponse(result));
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    [HttpPut]
    public async Task<ActionResult<ApiDtos.GlobalConfigurationResponse>> UpdateGlobalConfiguration(ApiDtos.UpdateGlobalConfigurationRequest request)
    {
        try
        {
            var command = new AppGlobalConfig.UpdateGlobalConfigurationCommand(
                request.BaseUrl,
                request.RequestIdKey,
                request.DownstreamScheme,
                request.Timeout,
                request.RateLimit != null ? new DomainRateLimitConfig
                {
                    EnableRateLimiting = request.RateLimit.EnableRateLimiting,
                    HttpStatusCode = request.RateLimit.HttpStatusCode
                } : null,
                request.QoS != null ? new DomainQoSConfig
                {
                    TimeoutValue = request.QoS.TimeoutValue,
                    DurationOfBreak = request.QoS.DurationOfBreak
                } : null,
                request.HttpHandler != null ? new DomainHttpHandlerConfig
                {
                    UseProxy = request.HttpHandler.UseProxy,
                    Expect100Continue = request.HttpHandler.Expect100Continue,
                    MaxConnectionsPerServer = request.HttpHandler.MaxConnectionsPerServer
                } : null,
                request.ServiceDiscovery != null ? new DomainServiceDiscoveryConfig
                {
                    Provider = request.ServiceDiscovery.Provider,
                    Host = request.ServiceDiscovery.Host,
                    Port = request.ServiceDiscovery.Port,
                    Type = request.ServiceDiscovery.Type,
                    Configuration = request.ServiceDiscovery.Configuration ?? new Dictionary<string, string>()
                } : null,
                User.Identity?.Name ?? "system"
            );

            var result = await _updateGlobalConfigurationCommandHandler.HandleAsync(command);
            return HandleResult(MapToResponse(result));
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    private static ApiDtos.GlobalConfigurationResponse MapToResponse(AppGlobalConfig.GlobalConfigurationResponse config)
    {
        return new ApiDtos.GlobalConfigurationResponse(
            config.Id,
            config.BaseUrl,
            config.RequestIdKey,
            config.DownstreamScheme,
            config.Timeout,
            config.RateLimit != null ? new ApiDtos.RateLimitConfigResponse(config.RateLimit.EnableRateLimiting, config.RateLimit.HttpStatusCode) : null,
            config.QoS != null ? new ApiDtos.QoSConfigResponse(config.QoS.TimeoutValue, config.QoS.DurationOfBreak) : null,
            config.HttpHandler != null ? new ApiDtos.HttpHandlerConfigResponse(config.HttpHandler.UseProxy, config.HttpHandler.Expect100Continue, config.HttpHandler.MaxConnectionsPerServer) : null,
            config.ServiceDiscovery != null ? new ApiDtos.ServiceDiscoveryConfigResponse(
                config.ServiceDiscovery.Provider,
                config.ServiceDiscovery.Host,
                config.ServiceDiscovery.Port,
                config.ServiceDiscovery.Type,
                config.ServiceDiscovery.Configuration ?? new Dictionary<string, string>()) : null,
            config.UpdatedAt
        );
    }
}