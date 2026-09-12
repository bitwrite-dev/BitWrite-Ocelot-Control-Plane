using BitWrite.OcelotControl.Api.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace BitWrite.OcelotControl.Api.Controllers;

[ApiController]
[Route("api/v1/plugins")]
public class PluginsController : BaseApiController
{
    [HttpGet]
    public async Task<ActionResult<PluginListResponse>> GetPlugins(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            // TODO: Implement using UseCase handler
            var response = new PluginListResponse(
                new List<PluginResponse>(),
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

    [HttpPost]
    public async Task<ActionResult<PluginResponse>> InstallPlugin(InstallPluginRequest request)
    {
        try
        {
            // TODO: Implement using UseCase handler
            var response = new PluginResponse(
                request.Id,
                request.Name,
                request.Version,
                request.Description,
                request.Scope,
                true,
                DateTimeOffset.UtcNow,
                null,
                null,
                null
            );
            return HandleResult(response);
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<PluginResponse>> GetPlugin(string id)
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

    [HttpPatch("{id}/enable")]
    public async Task<ActionResult<PluginResponse>> EnablePlugin(string id)
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

    [HttpPatch("{id}/disable")]
    public async Task<ActionResult<PluginResponse>> DisablePlugin(string id)
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

    [HttpDelete("{id}")]
    public async Task<ActionResult> UninstallPlugin(string id)
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