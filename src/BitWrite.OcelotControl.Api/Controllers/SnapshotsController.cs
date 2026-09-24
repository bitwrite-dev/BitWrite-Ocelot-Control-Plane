using ApiDtos = BitWrite.OcelotControl.Api.DTOs;
using AppSnapshot = BitWrite.OcelotControl.Application.UseCases.Snapshot;
using AppPublication = BitWrite.OcelotControl.Application.UseCases.Publication;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Domain.ValueObjects.Status;
using Microsoft.AspNetCore.Mvc;

namespace BitWrite.OcelotControl.Api.Controllers;

[ApiController]
[Route("api/v1/snapshots")]
public class SnapshotsController : BaseApiController
{
    private readonly AppSnapshot.CreateSnapshotCommandHandler _createSnapshotCommandHandler;
    private readonly AppSnapshot.GetSnapshotQueryHandler _getSnapshotQueryHandler;
    private readonly AppSnapshot.ListSnapshotsQueryHandler _listSnapshotsQueryHandler;
    private readonly AppSnapshot.ValidateSnapshotCommandHandler _validateSnapshotCommandHandler;
    private readonly AppSnapshot.CompareSnapshotsQueryHandler _compareSnapshotsQueryHandler;
    private readonly AppSnapshot.CloneSnapshotCommandHandler _cloneSnapshotCommandHandler;
    private readonly AppSnapshot.ExportSnapshotQueryHandler _exportSnapshotQueryHandler;
    private readonly AppSnapshot.GetSnapshotDeploymentQueryHandler _getSnapshotDeploymentQueryHandler;
    private readonly AppPublication.PublishSnapshotCommandHandler _publishSnapshotCommandHandler;
    private readonly AppPublication.RollbackSnapshotCommandHandler _rollbackSnapshotCommandHandler;

    public SnapshotsController(
        AppSnapshot.CreateSnapshotCommandHandler createSnapshotCommandHandler,
        AppSnapshot.GetSnapshotQueryHandler getSnapshotQueryHandler,
        AppSnapshot.ListSnapshotsQueryHandler listSnapshotsQueryHandler,
        AppSnapshot.ValidateSnapshotCommandHandler validateSnapshotCommandHandler,
        AppSnapshot.CompareSnapshotsQueryHandler compareSnapshotsQueryHandler,
        AppSnapshot.CloneSnapshotCommandHandler cloneSnapshotCommandHandler,
        AppSnapshot.ExportSnapshotQueryHandler exportSnapshotQueryHandler,
        AppSnapshot.GetSnapshotDeploymentQueryHandler getSnapshotDeploymentQueryHandler,
        AppPublication.PublishSnapshotCommandHandler publishSnapshotCommandHandler,
        AppPublication.RollbackSnapshotCommandHandler rollbackSnapshotCommandHandler)
    {
        _createSnapshotCommandHandler = createSnapshotCommandHandler;
        _getSnapshotQueryHandler = getSnapshotQueryHandler;
        _listSnapshotsQueryHandler = listSnapshotsQueryHandler;
        _validateSnapshotCommandHandler = validateSnapshotCommandHandler;
        _compareSnapshotsQueryHandler = compareSnapshotsQueryHandler;
        _cloneSnapshotCommandHandler = cloneSnapshotCommandHandler;
        _exportSnapshotQueryHandler = exportSnapshotQueryHandler;
        _getSnapshotDeploymentQueryHandler = getSnapshotDeploymentQueryHandler;
        _publishSnapshotCommandHandler = publishSnapshotCommandHandler;
        _rollbackSnapshotCommandHandler = rollbackSnapshotCommandHandler;
    }

    [HttpGet]
    public async Task<ActionResult<ApiDtos.SnapshotListResponse>> GetSnapshots(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null)
    {
        try
        {
            var query = new AppSnapshot.ListSnapshotsQuery(page, pageSize, status != null ? SnapshotStatus.From(status) : null);
            var result = await _listSnapshotsQueryHandler.HandleAsync(query);

            var response = new ApiDtos.SnapshotListResponse(
                result.Snapshots.Select(MapToResponse).ToList(),
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
    public async Task<ActionResult<ApiDtos.SnapshotResponse>> CreateSnapshot(ApiDtos.CreateSnapshotRequest request)
    {
        try
        {
            var command = new AppSnapshot.CreateSnapshotCommand(request.InitiatedBy, request.CorrelationId ?? "");
            var version = await _createSnapshotCommandHandler.HandleAsync(command);

            // Fetch the created snapshot
            var query = new AppSnapshot.GetSnapshotQuery(version);
            var snapshot = await _getSnapshotQueryHandler.HandleAsync(query);

            if (snapshot == null)
                return NotFound();

            return HandleResult(MapToResponse(snapshot));
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    [HttpPost("validate")]
    public async Task<ActionResult<ApiDtos.SnapshotValidationResponse>> ValidateSnapshot([FromBody] string content, [FromQuery] string initiatedBy = "system")
    {
        try
        {
            var command = new AppSnapshot.ValidateSnapshotCommand(content, initiatedBy);
            var result = await _validateSnapshotCommandHandler.HandleAsync(command);
            return HandleResult(new ApiDtos.SnapshotValidationResponse(result.IsValid, result.Errors.ToList()));
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    [HttpGet("{version}")]
    public async Task<ActionResult<ApiDtos.SnapshotResponse>> GetSnapshot(int version)
    {
        try
        {
            var query = new AppSnapshot.GetSnapshotQuery(SnapshotVersion.From(version));
            var result = await _getSnapshotQueryHandler.HandleAsync(query);

            if (result == null)
                return NotFound();

            return HandleResult(MapToResponse(result));
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    [HttpGet("{version}/compare")]
    public async Task<ActionResult<ApiDtos.SnapshotCompareResponse>> CompareSnapshots(int version, [FromQuery] int compareWith)
    {
        try
        {
            var query = new AppSnapshot.CompareSnapshotsQuery(SnapshotVersion.From(version), SnapshotVersion.From(compareWith));
            var result = await _compareSnapshotsQueryHandler.HandleAsync(query);
            return HandleResult(new ApiDtos.SnapshotCompareResponse(result.VersionA.Value, result.VersionB.Value, result.Differences.ToList()));
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    [HttpPost("{version}/clone")]
    public async Task<ActionResult<ApiDtos.SnapshotResponse>> CloneSnapshot(int version, ApiDtos.SnapshotCloneRequest request)
    {
        try
        {
            var command = new AppSnapshot.CloneSnapshotCommand(
                SnapshotVersion.From(version),
                request.NewName,
                request.InitiatedBy
            );

            var result = await _cloneSnapshotCommandHandler.HandleAsync(command);

            if (result == null)
                return NotFound();

            // CloneSnapshotResponse has NewVersion, ClonedFromVersion, Name
            // Need to fetch the actual snapshot to get full details
            var getQuery = new AppSnapshot.GetSnapshotQuery(result.NewVersion);
            var snapshot = await _getSnapshotQueryHandler.HandleAsync(getQuery);
            if (snapshot == null)
                return NotFound();

            return HandleResult(MapToResponse(snapshot));
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    [HttpGet("{version}/export")]
    public async Task<ActionResult<ApiDtos.SnapshotResponse>> ExportSnapshot(int version)
    {
        try
        {
            var query = new AppSnapshot.ExportSnapshotQuery(SnapshotVersion.From(version));
            var result = await _exportSnapshotQueryHandler.HandleAsync(query);

            if (result == null)
                return NotFound();

            // For export, we return the snapshot content in a special format
            // The ExportSnapshotResponse has Version, Content, Format
            return HandleResult(new ApiDtos.SnapshotResponse(
                result.Version.Value,
                "", // Hash not available in export
                result.Content,
                "Exported",
                "System",
                DateTimeOffset.UtcNow,
                null,
                null
            ));
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    [HttpPost("{version}/publish")]
    public async Task<ActionResult<ApiDtos.SnapshotDeploymentResponse>> PublishSnapshot(int version, ApiDtos.SnapshotPublishRequest request)
    {
        try
        {
            var command = new AppPublication.PublishSnapshotCommand(
                version.ToString(),
                request.InitiatedBy,
                CorrelationId: "",
                request.TargetGatewayIds
            );

            var publicationId = await _publishSnapshotCommandHandler.HandleAsync(command);

            var response = new ApiDtos.SnapshotDeploymentResponse(
                publicationId.Value.ToString(),
                version,
                "Started",
                DateTimeOffset.UtcNow,
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

    [HttpPost("{version}/rollback")]
    public async Task<ActionResult<ApiDtos.SnapshotDeploymentResponse>> RollbackSnapshot(int version, ApiDtos.SnapshotRollbackRequest request)
    {
        try
        {
            var command = new AppPublication.RollbackSnapshotCommand(
                request.TargetVersion.ToString(),
                request.InitiatedBy,
                CorrelationId: "",
                request.Reason
            );

            var publicationId = await _rollbackSnapshotCommandHandler.HandleAsync(command);

            var response = new ApiDtos.SnapshotDeploymentResponse(
                publicationId.Value.ToString(),
                request.TargetVersion,
                "Started",
                DateTimeOffset.UtcNow,
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

    [HttpGet("{version}/deployment")]
    public async Task<ActionResult<ApiDtos.SnapshotDeploymentResponse>> GetSnapshotDeployment(int version)
    {
        try
        {
            var query = new AppSnapshot.GetSnapshotDeploymentQuery(SnapshotVersion.From(version));
            var result = await _getSnapshotDeploymentQueryHandler.HandleAsync(query);

            if (result == null)
                return NotFound();

            return HandleResult(new ApiDtos.SnapshotDeploymentResponse(
                result.PublicationId,
                result.SnapshotVersion.Value,
                result.Status,
                result.StartedAt,
                result.CompletedAt,
                result.FailureReason
            ));
        }
        catch (Exception ex)
        {
            return HandleError(ex);
        }
    }

    private static ApiDtos.SnapshotResponse MapToResponse(AppSnapshot.SnapshotResponse snapshot)
    {
        return new ApiDtos.SnapshotResponse(
            snapshot.Version.Value,
            snapshot.Hash.Value,
            snapshot.Content,
            snapshot.Status.Value,
            snapshot.CreatedBy,
            snapshot.CreatedAt,
            snapshot.PublishedAt,
            snapshot.ArchivedAt
        );
    }
}