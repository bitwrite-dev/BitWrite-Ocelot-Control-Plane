using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BitWrite.OcelotControl.Application.UseCases.SystemSettings;

namespace BitWrite.OcelotControl.Api.Controllers;

/// <summary>
/// System settings: the operational configuration the control plane owns, and
/// the one-time choice of Ocelot version.
/// </summary>
/// <remarks>
/// Kept separate from <c>GlobalConfiguration</c>, which describes what is
/// published to gateways rather than how the control plane itself behaves, and
/// which already has its own page and endpoints.
/// </remarks>
[ApiController]
[Route("api/v1/settings")]
public class SettingsController : BaseApiController
{
    private readonly SystemSettingsCommandHandler _handler;

    public SettingsController(SystemSettingsCommandHandler handler)
    {
        _handler = handler;
    }

    /// <summary>
    /// The current settings, and whether first-run setup is still outstanding.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(SystemSettingsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<SystemSettingsResponse>> Get(
        [FromQuery] GetSystemSettingsQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await _handler.HandleAsync(query, cancellationToken));
    }

    /// <summary>
    /// Chooses the Ocelot version, once, and completes first-run setup.
    /// </summary>
    /// <remarks>
    /// Admin-only, because it is permanent: the version determines the shape of
    /// every configuration this installation will ever generate, and there is no
    /// way to undo the choice.
    /// <para>
    /// Submitting the same version again is accepted and idempotent. Two operators
    /// racing through first-run is a real scenario, and having the second one told
    /// "already chosen, cannot be changed" would leave them unable to tell whether
    /// their own submission was the one that landed.
    /// </para>
    /// </remarks>
    [HttpPost("first-run")]
    [Authorize(Policy = "Admin")]
    [ProducesResponseType(typeof(SystemSettingsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SystemSettingsResponse>> CompleteFirstRun(
        [FromBody] CompleteFirstRunRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CompleteFirstRunCommand(
            request.OcelotVersion,
            request.PollIntervalSeconds,
            request.AuditLogRetentionDays,
            request.SnapshotRetentionCount,
            InitiatedBy,
            CorrelationId);

        return Ok(await _handler.HandleAsync(command, cancellationToken));
    }

    /// <summary>
    /// Updates the settings that can change after setup. The version is not among
    /// them, and has no update path.
    /// </summary>
    [HttpPut]
    [Authorize(Policy = "Admin")]
    [ProducesResponseType(typeof(SystemSettingsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<SystemSettingsResponse>> Update(
        [FromBody] UpdateSystemSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateSystemSettingsCommand(
            request.PollIntervalSeconds,
            request.AuditLogRetentionDays,
            request.SnapshotRetentionCount,
            InitiatedBy,
            CorrelationId);

        return Ok(await _handler.HandleAsync(command, cancellationToken));
    }

    /// <summary>Who the request is attributed to in the audit trail.</summary>
    private string InitiatedBy =>
        User.Identity?.Name
        ?? User.Claims.FirstOrDefault(claim => claim.Type == "name")?.Value
        ?? "unknown";
}

public record CompleteFirstRunRequest(
    [property: Required][property: MaxLength(20)] string OcelotVersion,
    [property: Range(5, 3600)] int? PollIntervalSeconds = null,
    [property: Range(0, 3650)] int? AuditLogRetentionDays = null,
    [property: Range(0, int.MaxValue)] int? SnapshotRetentionCount = null
);

public record UpdateSystemSettingsRequest(
    [property: Range(5, 3600)] int? PollIntervalSeconds = null,
    [property: Range(0, 3650)] int? AuditLogRetentionDays = null,
    [property: Range(0, int.MaxValue)] int? SnapshotRetentionCount = null
);
