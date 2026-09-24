using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace BitWrite.OcelotControl.SDK.Authentication;

public interface IJwtTokenProvider
{
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);
    void SetTokens(string accessToken, string? refreshToken = null, DateTimeOffset? expiresAt = null);
    bool HasValidToken();
    void ClearTokens();
}

public class JwtTokenProvider : IJwtTokenProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<JwtTokenProvider> _logger;
    private readonly JwtTokenOptions _options;

    private string? _accessToken;
    private string? _refreshToken;
    private DateTimeOffset? _expiresAt;

    public JwtTokenProvider(
        HttpClient httpClient,
        ILogger<JwtTokenProvider> logger,
        JwtTokenOptions options)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options;
    }

    public void SetTokens(string accessToken, string? refreshToken = null, DateTimeOffset? expiresAt = null)
    {
        _accessToken = accessToken;
        _refreshToken = refreshToken;
        _expiresAt = expiresAt;
    }

    public bool HasValidToken()
    {
        if (string.IsNullOrEmpty(_accessToken))
            return false;

        if (_expiresAt.HasValue && DateTimeOffset.UtcNow >= _expiresAt.Value.AddMinutes(-5))
            return false;

        return true;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        if (HasValidToken())
        {
            return _accessToken!;
        }

        if (!string.IsNullOrEmpty(_refreshToken))
        {
            try
            {
                await RefreshTokenAsync(cancellationToken);
                return _accessToken!;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to refresh token, clearing tokens");
                ClearTokens();
            }
        }

        throw new InvalidOperationException("No valid access token available. Please authenticate first.");
    }

    private async Task RefreshTokenAsync(CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseAddress}/api/v1/auth/refresh")
        {
            Headers = { Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _refreshToken) }
        };

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var tokenResponse = System.Text.Json.JsonSerializer.Deserialize<TokenResponse>(content);

        if (tokenResponse?.AccessToken != null)
        {
            SetTokens(tokenResponse.AccessToken, tokenResponse.RefreshToken, tokenResponse.ExpiresAt);
        }
        else
        {
            throw new InvalidOperationException("Failed to refresh token");
        }
    }

    public void ClearTokens()
    {
        _accessToken = null;
        _refreshToken = null;
        _expiresAt = null;
    }

    private class TokenResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string? RefreshToken { get; set; }
        public DateTimeOffset? ExpiresAt { get; set; }
    }
}

public class JwtTokenOptions
{
    public string BaseAddress { get; set; } = string.Empty;
}