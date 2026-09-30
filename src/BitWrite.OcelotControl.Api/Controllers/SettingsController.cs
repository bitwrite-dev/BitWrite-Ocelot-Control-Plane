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
    /// The version is permanent: it determines the shape of every configuration
    /// this installation will ever generate, and there is no way to undo the
    /// choice. The domain refuses a second, different version regardless of who
    /// calls this.
    /// <para>
    /// A second submission is refused, including one carrying the version already
    /// chosen. An earlier comment here called that idempotent; the domain has
    /// always refused it, and refusing is the stricter reading for a choice that
    /// cannot be undone. A retry that arrives after a timeout is answered by
    /// reading the settings back, not by sending it again.
    /// </para>
    /// <para>
    /// Open to an unauthenticated request, deliberately. This endpoint requires a
    /// role, and the only role anyone can hold requires a token, and nothing in
    /// the product can issue a token yet (#433). Guarding it meant the one setting
    /// the product cannot run without was the one setting nobody could set: a fresh
    /// install answered 401 here, and every snapshot it tried to create refused to
    /// build because first-run had never completed. The two guards were arranged
    /// so that the first thing a new operator must do was the one thing they could
    /// not.
    /// <para>
    /// On a local installation the operator holds the machine, so there is nobody
    /// to authenticate at this point. The identity is taken from the request so the
    /// audit trail still records who chose, and setup itself records that it
    /// happened before authentication existed. Everything <em>after</em> setup keeps
    /// its policy.
    /// </para>
    /// </remarks>
    [HttpPost("first-run")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(SystemSettingsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
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
            SetupInitiatedBy(request),
            CorrelationId);

        return Ok(await _handler.HandleAsync(command, cancellationToken));
    }

    /// <summary>
    /// Who to attribute this one setup call to.
    /// </summary>
    /// <remarks>
    /// There is no token yet, so the claim path yields nothing and
    /// <c>InitiatedBy</c> would report "unknown" — an audit record that cannot say
    /// who made the permanent decision. The caller's own word is better than that,
    /// and the fallback says plainly that it came from setup rather than from a
    /// session, so nobody later reads it as an authenticated actor.
    /// </remarks>
    private string SetupInitiatedBy(CompleteFirstRunRequest request) =>
        InitiatedBy != Unattributed
            ? InitiatedBy
            : string.IsNullOrWhiteSpace(request.InitiatedBy)
                ? Unattributed
                : request.InitiatedBy;

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

    /// <summary>What the audit trail says when nothing identified the caller.</summary>
    private const string Unattributed = "unattributed";

    /// <summary>Who the request is attributed to in the audit trail.</summary>
    private string InitiatedBy =>
        User.Identity?.Name
        ?? User.Claims.FirstOrDefault(claim => claim.Type == "name")?.Value
        ?? Unattributed;
}

public record CompleteFirstRunRequest(
    [property: Required][property: MaxLength(20)] string OcelotVersion,
    [property: Range(5, 3600)] int? PollIntervalSeconds = null,
    [property: Range(0, 3650)] int? AuditLogRetentionDays = null,
    [property: Range(0, int.MaxValue)] int? SnapshotRetentionCount = null,
    /// <summary>
    /// Who is completing setup.
    /// </summary>
    /// <remarks>
    /// There is no session to read an identity from yet, so the caller supplies
    /// one. It is recorded in the audit trail against a decision that cannot be
    /// undone, which is the whole reason to ask for it.
    /// </remarks>
    [property: MaxLength(200)] string? InitiatedBy = null
);

public record UpdateSystemSettingsRequest(
    [property: Range(5, 3600)] int? PollIntervalSeconds = null,
    [property: Range(0, 3650)] int? AuditLogRetentionDays = null,
    [property: Range(0, int.MaxValue)] int? SnapshotRetentionCount = null
);
