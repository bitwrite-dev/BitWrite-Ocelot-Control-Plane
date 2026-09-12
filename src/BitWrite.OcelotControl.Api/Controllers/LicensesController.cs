using BitWrite.OcelotControl.Api.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace BitWrite.OcelotControl.Api.Controllers;

[ApiController]
[Route("api/v1/licenses")]
public class LicensesController : BaseApiController
{
    [HttpGet]
    public async Task<ActionResult<LicenseListResponse>> GetLicenses(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            // TODO: Implement using UseCase handler
            var response = new LicenseListResponse(
                new List<LicenseResponse>(),
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
    public async Task<ActionResult<LicenseResponse>> GetLicense(string id)
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
    public async Task<ActionResult<LicenseResponse>> ActivateLicense(ActivateLicenseRequest request)
    {
        try
        {
            // TODO: Implement using UseCase handler
            var response = new LicenseResponse(
                Guid.NewGuid().ToString(),
                "Enterprise",
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddYears(1),
                true,
                request.ActivatedBy
            );
            return HandleResult(response);
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }
}