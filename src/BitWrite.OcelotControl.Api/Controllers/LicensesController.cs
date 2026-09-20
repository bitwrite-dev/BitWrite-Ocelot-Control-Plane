using ApiDtos = BitWrite.OcelotControl.Api.DTOs;
using AppLicense = BitWrite.OcelotControl.Application.UseCases.License;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BitWrite.OcelotControl.Api.Controllers;

[ApiController]
[Route("api/v1/licenses")]
public class LicensesController : BaseApiController
{
    private readonly AppLicense.CreateLicenseCommandHandler _createLicenseCommandHandler;
    private readonly AppLicense.GetLicenseQueryHandler _getLicenseQueryHandler;
    private readonly AppLicense.ListLicensesQueryHandler _listLicensesQueryHandler;
    private readonly AppLicense.ActivateLicenseCommandHandler _activateLicenseCommandHandler;
    private readonly AppLicense.UpdateLicenseCommandHandler _updateLicenseCommandHandler;
    private readonly AppLicense.RenewLicenseCommandHandler _renewLicenseCommandHandler;
    private readonly AppLicense.RevokeLicenseCommandHandler _revokeLicenseCommandHandler;

    public LicensesController(
        AppLicense.CreateLicenseCommandHandler createLicenseCommandHandler,
        AppLicense.GetLicenseQueryHandler getLicenseQueryHandler,
        AppLicense.ListLicensesQueryHandler listLicensesQueryHandler,
        AppLicense.ActivateLicenseCommandHandler activateLicenseCommandHandler,
        AppLicense.UpdateLicenseCommandHandler updateLicenseCommandHandler,
        AppLicense.RenewLicenseCommandHandler renewLicenseCommandHandler,
        AppLicense.RevokeLicenseCommandHandler revokeLicenseCommandHandler)
    {
        _createLicenseCommandHandler = createLicenseCommandHandler;
        _getLicenseQueryHandler = getLicenseQueryHandler;
        _listLicensesQueryHandler = listLicensesQueryHandler;
        _activateLicenseCommandHandler = activateLicenseCommandHandler;
        _updateLicenseCommandHandler = updateLicenseCommandHandler;
        _renewLicenseCommandHandler = renewLicenseCommandHandler;
        _revokeLicenseCommandHandler = revokeLicenseCommandHandler;
    }

    [HttpGet]
    public async Task<ActionResult<ApiDtos.LicenseListResponse>> GetLicenses(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            var query = new AppLicense.ListLicensesQuery(page, pageSize);
            var result = await _listLicensesQueryHandler.HandleAsync(query);

            var response = new ApiDtos.LicenseListResponse(
                result.Licenses.Select(MapToResponse).ToList(),
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
    public async Task<ActionResult<ApiDtos.LicenseResponse>> GetLicense(string id)
    {
        try
        {
            var query = new AppLicense.GetLicenseQuery(LicenseId.From(Guid.Parse(id)));
            var result = await _getLicenseQueryHandler.HandleAsync(query);

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
    public async Task<ActionResult<ApiDtos.LicenseResponse>> CreateLicense(ApiDtos.CreateLicenseRequest request)
    {
        try
        {
            var command = new AppLicense.CreateLicenseCommand(
                request.Name,
                request.ProductCode,
                request.ExpirationDate,
                request.MaxGateways,
                request.MaxRoutes,
                request.InitiatedBy
            );

            var result = await _createLicenseCommandHandler.HandleAsync(command);
            return HandleResult(MapToResponse(result));
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    [HttpPost("activate")]
    public async Task<ActionResult<ApiDtos.LicenseResponse>> ActivateLicense(ApiDtos.ActivateLicenseRequest request)
    {
        try
        {
            var command = new AppLicense.ActivateLicenseCommand(
                request.LicenseKey,
                request.ActivatedBy
            );

            var result = await _activateLicenseCommandHandler.HandleAsync(command);
            if (result == null)
                return NotFound();

            // Map ActivateLicenseResponse to Api LicenseResponse
            var response = new ApiDtos.LicenseResponse(
                result.Id.Value.ToString(),
                "",
                result.ProductCode,
                "Active",
                result.ExpiresAt,
                result.ActivatedAt,
                result.ActivatedAt,
                true,
                result.ActivatedAt.ToString()
            );

            return HandleResult(response);
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ApiDtos.LicenseResponse>> UpdateLicense(string id, ApiDtos.UpdateLicenseRequest request)
    {
        try
        {
            var command = new AppLicense.UpdateLicenseCommand(
                LicenseId.From(Guid.Parse(id)),
                request.Name,
                request.Description
            );

            var result = await _updateLicenseCommandHandler.HandleAsync(command);
            return HandleResult(MapToResponse(result));
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    [HttpPut("{id}/renew")]
    public async Task<ActionResult<ApiDtos.LicenseResponse>> RenewLicense(string id, ApiDtos.RenewLicenseRequest request)
    {
        try
        {
            var command = new AppLicense.RenewLicenseCommand(
                LicenseId.From(Guid.Parse(id)),
                request.NewExpirationDate
            );

            var result = await _renewLicenseCommandHandler.HandleAsync(command);
            return HandleResult(MapToResponse(result));
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    [HttpDelete("{id}/revoke")]
    public async Task<ActionResult<ApiDtos.LicenseResponse>> RevokeLicense(string id, ApiDtos.RevokeLicenseRequest request)
    {
        try
        {
            var command = new AppLicense.RevokeLicenseCommand(
                LicenseId.From(Guid.Parse(id)),
                request.Reason
            );

            var result = await _revokeLicenseCommandHandler.HandleAsync(command);
            return HandleResult(MapToResponse(result));
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    private static ApiDtos.LicenseResponse MapToResponse(AppLicense.LicenseResponse license)
    {
        return new ApiDtos.LicenseResponse(
            license.Id.Value.ToString(),
            license.Name,
            license.ProductCode,
            license.Status.Value,
            license.ExpirationDate,
            license.CreatedAt,
            license.UpdatedAt,
            license.Status.IsActive,
            license.ActivatedAt?.ToString() ?? ""
        );
    }
}
