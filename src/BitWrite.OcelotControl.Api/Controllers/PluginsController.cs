using ApiDtos = BitWrite.OcelotControl.Api.DTOs;
using AppPlugin = BitWrite.OcelotControl.Application.UseCases.Plugin;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Domain.ValueObjects.Status;
using Microsoft.AspNetCore.Mvc;

namespace BitWrite.OcelotControl.Api.Controllers;

[ApiController]
[Route("api/v1/plugins")]
public class PluginsController : BaseApiController
{
    private readonly AppPlugin.InstallPluginCommandHandler _installPluginCommandHandler;
    private readonly AppPlugin.GetPluginQueryHandler _getPluginQueryHandler;
    private readonly AppPlugin.ListPluginsQueryHandler _listPluginsQueryHandler;
    private readonly AppPlugin.EnablePluginCommandHandler _enablePluginCommandHandler;
    private readonly AppPlugin.DisablePluginCommandHandler _disablePluginCommandHandler;
    private readonly AppPlugin.UninstallPluginCommandHandler _uninstallPluginCommandHandler;
    private readonly AppPlugin.UpgradePluginCommandHandler _upgradePluginCommandHandler;

    public PluginsController(
        AppPlugin.InstallPluginCommandHandler installPluginCommandHandler,
        AppPlugin.GetPluginQueryHandler getPluginQueryHandler,
        AppPlugin.ListPluginsQueryHandler listPluginsQueryHandler,
        AppPlugin.EnablePluginCommandHandler enablePluginCommandHandler,
        AppPlugin.DisablePluginCommandHandler disablePluginCommandHandler,
        AppPlugin.UninstallPluginCommandHandler uninstallPluginCommandHandler,
        AppPlugin.UpgradePluginCommandHandler upgradePluginCommandHandler)
    {
        _installPluginCommandHandler = installPluginCommandHandler;
        _getPluginQueryHandler = getPluginQueryHandler;
        _listPluginsQueryHandler = listPluginsQueryHandler;
        _enablePluginCommandHandler = enablePluginCommandHandler;
        _disablePluginCommandHandler = disablePluginCommandHandler;
        _uninstallPluginCommandHandler = uninstallPluginCommandHandler;
        _upgradePluginCommandHandler = upgradePluginCommandHandler;
    }

    [HttpGet]
    public async Task<ActionResult<ApiDtos.PluginListResponse>> GetPlugins(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            var query = new AppPlugin.PluginListQuery(page, pageSize);
            var result = await _listPluginsQueryHandler.HandleAsync(query);

            var response = new ApiDtos.PluginListResponse(
                result.Plugins.Select(MapToResponse).ToList(),
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

    [HttpPost]
    public async Task<ActionResult<ApiDtos.PluginResponse>> InstallPlugin(ApiDtos.InstallPluginRequest request)
    {
        try
        {
            var scope = PluginScope.From(request.Scope);

            var command = new AppPlugin.InstallPluginCommand(
                request.Id,
                request.Name,
                request.Version,
                request.Description,
                scope,
                User.Identity?.Name ?? "system"
            );

            var result = await _installPluginCommandHandler.HandleAsync(command);
            return HandleResult(MapToResponse(result));
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiDtos.PluginResponse>> GetPlugin(string id)
    {
        try
        {
            var query = new AppPlugin.GetPluginQuery(PluginId.From(id));
            var result = await _getPluginQueryHandler.HandleAsync(query);

            if (result == null)
                return NotFound();

            return HandleResult(MapToResponse(result));
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    [HttpPatch("{id}/enable")]
    public async Task<ActionResult<ApiDtos.PluginResponse>> EnablePlugin(string id)
    {
        try
        {
            var command = new AppPlugin.EnablePluginCommand(
                PluginId.From(id),
                User.Identity?.Name ?? "system"
            );

            var result = await _enablePluginCommandHandler.HandleAsync(command);
            return HandleResult(MapToResponse(result));
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    [HttpPatch("{id}/disable")]
    public async Task<ActionResult<ApiDtos.PluginResponse>> DisablePlugin(string id)
    {
        try
        {
            var command = new AppPlugin.DisablePluginCommand(
                PluginId.From(id),
                User.Identity?.Name ?? "system"
            );

            var result = await _disablePluginCommandHandler.HandleAsync(command);
            return HandleResult(MapToResponse(result));
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
            var command = new AppPlugin.UninstallPluginCommand(
                PluginId.From(id),
                User.Identity?.Name ?? "system"
            );

            await _uninstallPluginCommandHandler.HandleAsync(command);
            return NoContent();
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    [HttpPut("{id}/upgrade")]
    public async Task<ActionResult<ApiDtos.PluginResponse>> UpgradePlugin(string id, UpgradePluginRequest request)
    {
        try
        {
            var command = new AppPlugin.UpgradePluginCommand(
                PluginId.From(id),
                request.NewVersion,
                User.Identity?.Name ?? "system"
            );

            var result = await _upgradePluginCommandHandler.HandleAsync(command);
            return HandleResult(MapToResponse(result));
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    private static ApiDtos.PluginResponse MapToResponse(AppPlugin.PluginResponse plugin)
    {
        return new ApiDtos.PluginResponse(
            plugin.Id.Value,
            plugin.Name,
            plugin.Version,
            plugin.Description,
            plugin.Scope.Value,
            plugin.IsEnabled,
            plugin.InstalledAt,
            plugin.LastUpdated,
            plugin.LastEnabled,
            plugin.LastDisabled
        );
    }
}

public record UpgradePluginRequest(
    string NewVersion
);