using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using BitWrite.OcelotControl.SDK.Authentication;
using Microsoft.Extensions.Logging;

namespace BitWrite.OcelotControl.SDK;

public class OcelotControlClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly IJwtTokenProvider _tokenProvider;
    private readonly ILogger<OcelotControlClient> _logger;
    private readonly OcelotControlClientOptions _options;
    private bool _disposed;

    public OcelotControlClient(
        HttpClient httpClient,
        IJwtTokenProvider tokenProvider,
        ILogger<OcelotControlClient> logger,
        OcelotControlClientOptions options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));

        _httpClient.BaseAddress = new Uri(_options.BaseAddress);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<T> GetAsync<T>(string endpoint, CancellationToken cancellationToken = default)
    {
        var request = await CreateAuthenticatedRequestAsync(HttpMethod.Get, endpoint, cancellationToken);
        var response = await _httpClient.SendAsync(request, cancellationToken);
        return await HandleResponseAsync<T>(response, cancellationToken);
    }

    public async Task<T> PostAsync<T, TRequest>(string endpoint, TRequest request, CancellationToken cancellationToken = default)
    {
        var httpRequest = await CreateAuthenticatedRequestAsync(HttpMethod.Post, endpoint, cancellationToken);
        httpRequest.Content = CreateJsonContent(request);
        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        return await HandleResponseAsync<T>(response, cancellationToken);
    }

    public async Task<T> PutAsync<T, TRequest>(string endpoint, TRequest request, CancellationToken cancellationToken = default)
    {
        var httpRequest = await CreateAuthenticatedRequestAsync(HttpMethod.Put, endpoint, cancellationToken);
        httpRequest.Content = CreateJsonContent(request);
        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        return await HandleResponseAsync<T>(response, cancellationToken);
    }

    public async Task DeleteAsync(string endpoint, CancellationToken cancellationToken = default)
    {
        var request = await CreateAuthenticatedRequestAsync(HttpMethod.Delete, endpoint, cancellationToken);
        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<T> PatchAsync<T, TRequest>(string endpoint, TRequest request, CancellationToken cancellationToken = default)
    {
        var httpRequest = await CreateAuthenticatedRequestAsync(HttpMethod.Patch, endpoint, cancellationToken);
        httpRequest.Content = CreateJsonContent(request);
        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        return await HandleResponseAsync<T>(response, cancellationToken);
    }

    private async Task<HttpRequestMessage> CreateAuthenticatedRequestAsync(HttpMethod method, string endpoint, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(method, endpoint);

        var token = await _tokenProvider.GetAccessTokenAsync(cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return request;
    }

    private static HttpContent CreateJsonContent<T>(T content)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(content, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        });
        return new StringContent(json, System.Text.Encoding.UTF8, "application/json");
    }

    private async Task<T> HandleResponseAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("API request failed: {StatusCode} - {Content}", response.StatusCode, errorContent);
            throw new OcelotControlApiException($"API request failed: {response.StatusCode} - {errorContent}");
        }

        if (typeof(T) == typeof(void))
        {
            return default!;
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        return System.Text.Json.JsonSerializer.Deserialize<T>(content, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        })!;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _httpClient?.Dispose();
            _disposed = true;
        }
    }
}

public class OcelotControlClientOptions
{
    public string BaseAddress { get; set; } = string.Empty;
}

public class OcelotControlApiException : Exception
{
    public OcelotControlApiException(string message) : base(message) { }
    public OcelotControlApiException(string message, Exception inner) : base(message, inner) { }
}