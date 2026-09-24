using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BitWrite.OcelotControl.SDK.Models.Requests;
using BitWrite.OcelotControl.SDK.Models.Responses;

namespace BitWrite.OcelotControl.SDK.Clients;

public interface IServicesClient
{
    Task<PagedResponse<ServiceResponse>> GetServicesAsync(
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    Task<ServiceResponse?> GetServiceAsync(string id, CancellationToken cancellationToken = default);

    Task<ServiceResponse> CreateServiceAsync(CreateServiceRequest request, CancellationToken cancellationToken = default);

    Task<ServiceResponse> UpdateServiceAsync(string id, UpdateServiceRequest request, CancellationToken cancellationToken = default);

    Task DeleteServiceAsync(string id, CancellationToken cancellationToken = default);

    Task<ServiceRoutesResponse> GetServiceRoutesAsync(string id, CancellationToken cancellationToken = default);
}

public class ServicesClient : IServicesClient
{
    private readonly OcelotControlClient _client;

    public ServicesClient(OcelotControlClient client)
    {
        _client = client;
    }

    public async Task<PagedResponse<ServiceResponse>> GetServicesAsync(
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var queryParams = new List<string>
        {
            $"page={page}",
            $"pageSize={pageSize}"
        };

        var endpoint = $"api/v1/services?{string.Join("&", queryParams)}";
        return await _client.GetAsync<PagedResponse<ServiceResponse>>(endpoint, cancellationToken);
    }

    public async Task<ServiceResponse?> GetServiceAsync(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _client.GetAsync<ServiceResponse>($"api/v1/services/{Uri.EscapeDataString(id)}", cancellationToken);
        }
        catch (OcelotControlApiException ex) when (ex.Message.Contains("404"))
        {
            return null;
        }
    }

    public async Task<ServiceResponse> CreateServiceAsync(CreateServiceRequest request, CancellationToken cancellationToken = default)
    {
        return await _client.PostAsync<ServiceResponse, CreateServiceRequest>("api/v1/services", request, cancellationToken);
    }

    public async Task<ServiceResponse> UpdateServiceAsync(string id, UpdateServiceRequest request, CancellationToken cancellationToken = default)
    {
        return await _client.PutAsync<ServiceResponse, UpdateServiceRequest>($"api/v1/services/{Uri.EscapeDataString(id)}", request, cancellationToken);
    }

    public async Task DeleteServiceAsync(string id, CancellationToken cancellationToken = default)
    {
        await _client.DeleteAsync($"api/v1/services/{Uri.EscapeDataString(id)}", cancellationToken);
    }

    public async Task<ServiceRoutesResponse> GetServiceRoutesAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _client.GetAsync<ServiceRoutesResponse>($"api/v1/services/{Uri.EscapeDataString(id)}/routes", cancellationToken);
    }
}