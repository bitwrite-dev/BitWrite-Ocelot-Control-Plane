using ApiDtos = BitWrite.OcelotControl.Api.DTOs;
using AppService = BitWrite.OcelotControl.Application.UseCases.Service;
using DomainDownstreamTarget = BitWrite.OcelotControl.Domain.ValueObjects.Configuration.DownstreamTarget;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BitWrite.OcelotControl.Api.Controllers;

[ApiController]
[Route("api/v1/services")]
public class ServicesController : BaseApiController
{
    private readonly AppService.CreateServiceCommandHandler _createServiceCommandHandler;
    private readonly AppService.GetServiceQueryHandler _getServiceQueryHandler;
    private readonly AppService.ListServicesQueryHandler _listServicesQueryHandler;
    private readonly AppService.UpdateServiceCommandHandler _updateServiceCommandHandler;
    private readonly AppService.DeleteServiceCommandHandler _deleteServiceCommandHandler;
    private readonly AppService.GetServiceRoutesQueryHandler _getServiceRoutesQueryHandler;

    public ServicesController(
        AppService.CreateServiceCommandHandler createServiceCommandHandler,
        AppService.GetServiceQueryHandler getServiceQueryHandler,
        AppService.ListServicesQueryHandler listServicesQueryHandler,
        AppService.UpdateServiceCommandHandler updateServiceCommandHandler,
        AppService.DeleteServiceCommandHandler deleteServiceCommandHandler,
        AppService.GetServiceRoutesQueryHandler getServiceRoutesQueryHandler)
    {
        _createServiceCommandHandler = createServiceCommandHandler;
        _getServiceQueryHandler = getServiceQueryHandler;
        _listServicesQueryHandler = listServicesQueryHandler;
        _updateServiceCommandHandler = updateServiceCommandHandler;
        _deleteServiceCommandHandler = deleteServiceCommandHandler;
        _getServiceRoutesQueryHandler = getServiceRoutesQueryHandler;
    }

    [HttpGet]
    public async Task<ActionResult<ApiDtos.ServiceListResponse>> GetServices(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            var query = new AppService.ListServicesQuery(page, pageSize);
            var result = await _listServicesQueryHandler.HandleAsync(query);

            var response = new ApiDtos.ServiceListResponse(
                result.Services.Select(MapToResponse).ToList(),
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
    public async Task<ActionResult<ApiDtos.ServiceResponse>> GetService(string id)
    {
        try
        {
            var query = new AppService.GetServiceQuery(ServiceId.From(Guid.Parse(id)));
            var result = await _getServiceQueryHandler.HandleAsync(query);

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
    public async Task<ActionResult<ApiDtos.ServiceResponse>> CreateService(ApiDtos.CreateServiceRequest request)
    {
        try
        {
            var command = new AppService.CreateServiceCommand(
                request.Name,
                request.Description,
                request.DownstreamTargets?.Select(MapToDownstreamTarget).ToList(),
                User.Identity?.Name ?? "system"
            );

            var result = await _createServiceCommandHandler.HandleAsync(command);
            return HandleResult(MapToResponse(result));
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ApiDtos.ServiceResponse>> UpdateService(string id, ApiDtos.UpdateServiceRequest request)
    {
        try
        {
            var command = new AppService.UpdateServiceCommand(
                ServiceId.From(Guid.Parse(id)),
                request.Name,
                request.Description,
                request.DownstreamTargets?.Select(MapToDownstreamTarget).ToList(),
                User.Identity?.Name ?? "system"
            );

            var result = await _updateServiceCommandHandler.HandleAsync(command);
            return HandleResult(MapToResponse(result));
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteService(string id)
    {
        try
        {
            var command = new AppService.DeleteServiceCommand(
                ServiceId.From(Guid.Parse(id)),
                User.Identity?.Name ?? "system"
            );

            await _deleteServiceCommandHandler.HandleAsync(command);
            return NoContent();
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    [HttpGet("{id}/routes")]
    public async Task<ActionResult<ApiDtos.ServiceRoutesResponse>> GetServiceRoutes(string id)
    {
        try
        {
            var query = new AppService.GetServiceRoutesQuery(ServiceId.From(Guid.Parse(id)));
            var result = await _getServiceRoutesQueryHandler.HandleAsync(query);

            var response = new ApiDtos.ServiceRoutesResponse(
                result.Routes.Select(MapToRouteResponse).ToList()
            );

            return HandleResult(response);
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    private static DomainDownstreamTarget MapToDownstreamTarget(ApiDtos.DownstreamTargetRequest request)
    {
        return DomainDownstreamTarget.Create(request.Scheme, request.Host, request.Port, request.Path);
    }

    private static ApiDtos.ServiceResponse MapToResponse(AppService.ServiceResponse service)
    {
        return new ApiDtos.ServiceResponse(
            service.Id.Value.ToString(),
            service.Name,
            service.Description,
            service.Endpoints.Select(e => new ApiDtos.DownstreamTargetResponse(e.Host, e.Port, "http", "/")).ToList(),
            service.CreatedAt,
            service.UpdatedAt
        );
    }

    private static ApiDtos.RouteResponse MapToRouteResponse(AppService.RouteSummary route)
    {
        return new ApiDtos.RouteResponse(
            route.Id.Value.ToString(),
            route.Key,
            route.Method,
            route.UpstreamPath,
            null,
            "",
            true,
            new List<ApiDtos.DownstreamTargetResponse>(),
            null,
            null,
            null,
            null,
            null,
            DateTimeOffset.MinValue,
            DateTimeOffset.MinValue
        );
    }
}