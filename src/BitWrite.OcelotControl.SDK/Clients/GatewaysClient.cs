using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BitWrite.OcelotControl.SDK.Models.Requests;
using BitWrite.OcelotControl.SDK.Models.Responses;

namespace BitWrite.OcelotControl.SDK.Clients;

public interface IGatewaysClient
{
    Task<PagedResponse<GatewayResponse>> GetGatewaysAsync(int page = 1, int pageSize = 20, CancellationToken cancellationToken = default);
    Task<GatewayResponse> GetGatewayAsync(string id, CancellationToken cancellationToken = default);
    Task<GatewayResponse> CreateGatewayAsync(CreateGatewayRequest request, CancellationToken cancellationToken = default);
    Task<GatewayResponse> UpdateGatewayAsync(string id, UpdateGatewayRequest request, CancellationToken cancellationToken = default);
    Task<GatewayResponse> UpdateGatewayStatusAsync(string id, UpdateGatewayStatusRequest request, CancellationToken cancellationToken = default);
}

public class GatewaysClient : IGatewaysClient
{
    private readonly OcelotControlClient _client;

    public GatewaysClient(OcelotControlClient client)
    {
        _client = client;
    }

    public async Task<PagedResponse<GatewayResponse>> GetGatewaysAsync(int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var endpoint = $"api/v1/gateways?page={page}&pageSize={pageSize}";
        return await _client.GetAsync<PagedResponse<GatewayResponse>>(endpoint, cancellationToken);
    }

    public async Task<GatewayResponse> GetGatewayAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _client.GetAsync<GatewayResponse>($"api/v1/gateways/{id}", cancellationToken);
    }

    public async Task<GatewayResponse> CreateGatewayAsync(CreateGatewayRequest request, CancellationToken cancellationToken = default)
    {
        return await _client.PostAsync<GatewayResponse, CreateGatewayRequest>("api/v1/gateways", request, cancellationToken);
    }

    public async Task<GatewayResponse> UpdateGatewayAsync(string id, UpdateGatewayRequest request, CancellationToken cancellationToken = default)
    {
        return await _client.PutAsync<GatewayResponse, UpdateGatewayRequest>($"api/v1/gateways/{id}", request, cancellationToken);
    }

    public async Task<GatewayResponse> UpdateGatewayStatusAsync(string id, UpdateGatewayStatusRequest request, CancellationToken cancellationToken = default)
    {
        return await _client.PatchAsync<GatewayResponse, UpdateGatewayStatusRequest>($"api/v1/gateways/{id}/status", request, cancellationToken);
    }
}