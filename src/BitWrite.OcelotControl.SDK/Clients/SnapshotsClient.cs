using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BitWrite.OcelotControl.SDK.Models.Requests;
using BitWrite.OcelotControl.SDK.Models.Responses;

namespace BitWrite.OcelotControl.SDK.Clients;

public interface ISnapshotsClient
{
    Task<PagedResponse<SnapshotResponse>> GetSnapshotsAsync(
        int page = 1,
        int pageSize = 20,
        string? status = null,
        CancellationToken cancellationToken = default);

    Task<SnapshotResponse?> GetSnapshotAsync(int version, CancellationToken cancellationToken = default);

    Task<SnapshotResponse> CreateSnapshotAsync(CreateSnapshotRequest request, CancellationToken cancellationToken = default);

    Task<SnapshotValidationResponse> ValidateSnapshotAsync(ValidateSnapshotRequest request, CancellationToken cancellationToken = default);

    Task<SnapshotCompareResponse> CompareSnapshotsAsync(int version, int compareWith, CancellationToken cancellationToken = default);

    Task<SnapshotResponse> CloneSnapshotAsync(CloneSnapshotRequest request, CancellationToken cancellationToken = default);

    Task<ExportSnapshotResponse> ExportSnapshotAsync(ExportSnapshotRequest request, CancellationToken cancellationToken = default);

    Task<SnapshotDeploymentResponse> PublishSnapshotAsync(PublishSnapshotRequest request, CancellationToken cancellationToken = default);

    Task<SnapshotDeploymentResponse> RollbackSnapshotAsync(RollbackSnapshotRequest request, CancellationToken cancellationToken = default);

    Task<SnapshotDeploymentResponse?> GetSnapshotDeploymentAsync(int version, CancellationToken cancellationToken = default);
}

public class SnapshotsClient : ISnapshotsClient
{
    private readonly OcelotControlClient _client;

    public SnapshotsClient(OcelotControlClient client)
    {
        _client = client;
    }

    public async Task<PagedResponse<SnapshotResponse>> GetSnapshotsAsync(
        int page = 1,
        int pageSize = 20,
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        var queryParams = new List<string>
        {
            $"page={page}",
            $"pageSize={pageSize}"
        };

        if (!string.IsNullOrEmpty(status))
            queryParams.Add($"status={Uri.EscapeDataString(status)}");

        var endpoint = $"api/v1/snapshots?{string.Join("&", queryParams)}";
        return await _client.GetAsync<PagedResponse<SnapshotResponse>>(endpoint, cancellationToken);
    }

    public async Task<SnapshotResponse?> GetSnapshotAsync(int version, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _client.GetAsync<SnapshotResponse>($"api/v1/snapshots/{version}", cancellationToken);
        }
        catch (OcelotControlApiException ex) when (ex.Message.Contains("404"))
        {
            return null;
        }
    }

    public async Task<SnapshotResponse> CreateSnapshotAsync(CreateSnapshotRequest request, CancellationToken cancellationToken = default)
    {
        return await _client.PostAsync<SnapshotResponse, CreateSnapshotRequest>("api/v1/snapshots", request, cancellationToken);
    }

    public async Task<SnapshotValidationResponse> ValidateSnapshotAsync(ValidateSnapshotRequest request, CancellationToken cancellationToken = default)
    {
        return await _client.PostAsync<SnapshotValidationResponse, ValidateSnapshotRequest>("api/v1/snapshots/validate", request, cancellationToken);
    }

    public async Task<SnapshotCompareResponse> CompareSnapshotsAsync(int version, int compareWith, CancellationToken cancellationToken = default)
    {
        var queryParams = $"api/v1/snapshots/{version}/compare?compareWith={compareWith}";
        return await _client.GetAsync<SnapshotCompareResponse>(queryParams, cancellationToken);
    }

    public async Task<SnapshotResponse> CloneSnapshotAsync(CloneSnapshotRequest request, CancellationToken cancellationToken = default)
    {
        return await _client.PostAsync<SnapshotResponse, CloneSnapshotRequest>($"api/v1/snapshots/{request.Version}/clone", request, cancellationToken);
    }

    public async Task<ExportSnapshotResponse> ExportSnapshotAsync(ExportSnapshotRequest request, CancellationToken cancellationToken = default)
    {
        return await _client.GetAsync<ExportSnapshotResponse>($"api/v1/snapshots/{request.Version}/export", cancellationToken);
    }

    public async Task<SnapshotDeploymentResponse> PublishSnapshotAsync(PublishSnapshotRequest request, CancellationToken cancellationToken = default)
    {
        return await _client.PostAsync<SnapshotDeploymentResponse, PublishSnapshotRequest>($"api/v1/snapshots/{request.Version}/publish", request, cancellationToken);
    }

    public async Task<SnapshotDeploymentResponse> RollbackSnapshotAsync(RollbackSnapshotRequest request, CancellationToken cancellationToken = default)
    {
        return await _client.PostAsync<SnapshotDeploymentResponse, RollbackSnapshotRequest>($"api/v1/snapshots/{request.TargetVersion}/rollback", request, cancellationToken);
    }

    public async Task<SnapshotDeploymentResponse?> GetSnapshotDeploymentAsync(int version, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _client.GetAsync<SnapshotDeploymentResponse>($"api/v1/snapshots/{version}/deployment", cancellationToken);
        }
        catch (OcelotControlApiException ex) when (ex.Message.Contains("404"))
        {
            return null;
        }
    }
}