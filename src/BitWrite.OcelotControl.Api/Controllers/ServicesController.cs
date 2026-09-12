using BitWrite.OcelotControl.Api.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace BitWrite.OcelotControl.Api.Controllers;

[ApiController]
[Route("api/v1/services")]
public class ServicesController : BaseApiController
{
    [HttpGet]
    public async Task<ActionResult<ServiceListResponse>> GetServices(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            // TODO: Implement using UseCase handler
            var response = new ServiceListResponse(
                new List<ServiceResponse>(),
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
    public async Task<ActionResult<ServiceResponse>> GetService(string id)
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
    public async Task<ActionResult<ServiceResponse>> CreateService(CreateServiceRequest request)
    {
        try
        {
            // TODO: Implement using UseCase handler
            var response = new ServiceResponse(
                Guid.NewGuid().ToString(),
                request.Name,
                request.Description,
                request.DownstreamTargets?.Select(t => new DownstreamTargetResponse(t.Host, t.Port, t.Scheme, t.Path)).ToList() ?? new(),
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
    public async Task<ActionResult<ServiceResponse>> UpdateService(string id, UpdateServiceRequest request)
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
    public async Task<ActionResult> DeleteService(string id)
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

    [HttpGet("{id}/routes")]
    public async Task<ActionResult<ServiceRoutesResponse>> GetServiceRoutes(string id)
    {
        try
        {
            // TODO: Implement using UseCase handler
            var response = new ServiceRoutesResponse(new List<RouteResponse>());
            return HandleResult(response);
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }
}