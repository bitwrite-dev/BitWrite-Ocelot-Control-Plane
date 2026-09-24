using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BitWrite.OcelotControl.SDK.Models.Requests;
using BitWrite.OcelotControl.SDK.Models.Responses;

namespace BitWrite.OcelotControl.SDK.Clients;

public interface IRoutesClient
{
    Task<PagedResponse<RouteResponse>> GetRoutesAsync(
        int page = 1,
        int pageSize = 20,
        string? serviceId = null,
        bool? isEnabled = null,
        string? search = null,
        CancellationToken cancellationToken = default);

    Task<RouteResponse?> GetRouteAsync(string id, CancellationToken cancellationToken = default);

    Task<RouteResponse> CreateRouteAsync(CreateRouteRequest request, CancellationToken cancellationToken = default);

    Task<RouteResponse> UpdateRouteAsync(string id, UpdateRouteRequest request, CancellationToken cancellationToken = default);

    Task<RouteResponse> EnableRouteAsync(string id, CancellationToken cancellationToken = default);

    Task<RouteResponse> DisableRouteAsync(string id, CancellationToken cancellationToken = default);

    Task DeleteRouteAsync(string id, CancellationToken cancellationToken = default);

    Task<RouteValidationResponse> ValidateRouteAsync(string id, CancellationToken cancellationToken = default);

    Task<RoutePreviewResponse> PreviewRouteAsync(string id, CancellationToken cancellationToken = default);

    Task<RouteEffectiveResponse> GetEffectiveRouteAsync(string id, CancellationToken cancellationToken = default);

    Task<RouteHistoryResponse> GetRouteHistoryAsync(string id, CancellationToken cancellationToken = default);
}

public class RoutesClient : IRoutesClient
{
    private readonly OcelotControlClient _client;

    public RoutesClient(OcelotControlClient client)
    {
        _client = client;
    }

    public async Task<PagedResponse<RouteResponse>> GetRoutesAsync(
        int page = 1,
        int pageSize = 20,
        string? serviceId = null,
        bool? isEnabled = null,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var queryParams = new List<string>
        {
            $"page={page}",
            $"pageSize={pageSize}"
        };

        if (!string.IsNullOrEmpty(serviceId))
            queryParams.Add($"serviceId={Uri.EscapeDataString(serviceId)}");
        if (isEnabled.HasValue)
            queryParams.Add($"isEnabled={isEnabled.Value.ToString().ToLower()}");
        if (!string.IsNullOrEmpty(search))
            queryParams.Add($"search={Uri.EscapeDataString(search)}");

        var endpoint = $"api/v1/routes?{string.Join("&", queryParams)}";
        return await _client.GetAsync<PagedResponse<RouteResponse>>(endpoint, cancellationToken);
    }

    public async Task<RouteResponse?> GetRouteAsync(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _client.GetAsync<RouteResponse>($"api/v1/routes/{Uri.EscapeDataString(id)}", cancellationToken);
        }
        catch (OcelotControlApiException ex) when (ex.Message.Contains("404"))
        {
            return null;
        }
    }

    public async Task<RouteResponse> CreateRouteAsync(CreateRouteRequest request, CancellationToken cancellationToken = default)
    {
        return await _client.PostAsync<RouteResponse, CreateRouteRequest>("api/v1/routes", request, cancellationToken);
    }

    public async Task<RouteResponse> UpdateRouteAsync(string id, UpdateRouteRequest request, CancellationToken cancellationToken = default)
    {
        return await _client.PutAsync<RouteResponse, UpdateRouteRequest>($"api/v1/routes/{Uri.EscapeDataString(id)}", request, cancellationToken);
    }

    public async Task<RouteResponse> EnableRouteAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _client.PatchAsync<RouteResponse, object>($"api/v1/routes/{Uri.EscapeDataString(id)}/enable", new { }, cancellationToken);
    }

    public async Task<RouteResponse> DisableRouteAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _client.PatchAsync<RouteResponse, object>($"api/v1/routes/{Uri.EscapeDataString(id)}/disable", new { }, cancellationToken);
    }

    public async Task DeleteRouteAsync(string id, CancellationToken cancellationToken = default)
    {
        await _client.DeleteAsync($"api/v1/routes/{Uri.EscapeDataString(id)}", cancellationToken);
    }

    public async Task<RouteValidationResponse> ValidateRouteAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _client.PostAsync<RouteValidationResponse, object>($"api/v1/routes/{Uri.EscapeDataString(id)}/validate", new { }, cancellationToken);
    }

    public async Task<RoutePreviewResponse> PreviewRouteAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _client.GetAsync<RoutePreviewResponse>($"api/v1/routes/{Uri.EscapeDataString(id)}/preview", cancellationToken);
    }

    public async Task<RouteEffectiveResponse> GetEffectiveRouteAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _client.GetAsync<RouteEffectiveResponse>($"api/v1/routes/{Uri.EscapeDataString(id)}/effective", cancellationToken);
    }

    public async Task<RouteHistoryResponse> GetRouteHistoryAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _client.GetAsync<RouteHistoryResponse>($"api/v1/routes/{Uri.EscapeDataString(id)}/history", cancellationToken);
    }
}