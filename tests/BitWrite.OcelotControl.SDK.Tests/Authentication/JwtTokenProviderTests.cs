using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BitWrite.OcelotControl.SDK.Authentication;
using FluentAssertions;
using Moq;
using Moq.Protected;
using Xunit;

namespace BitWrite.OcelotControl.SDK.Tests.Authentication;

public class JwtTokenProviderTests
{
    private readonly Mock<HttpMessageHandler> _mockHttpHandler;
    private readonly HttpClient _httpClient;
    private readonly Mock<ILogger<JwtTokenProvider>> _mockLogger;
    private readonly JwtTokenOptions _options;
    private readonly JwtTokenProvider _provider;

    public JwtTokenProviderTests()
    {
        _mockHttpHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpHandler.Object);
        _mockLogger = new Mock<ILogger<JwtTokenProvider>>();
        _options = new JwtTokenOptions { BaseAddress = "https://api.example.com" };
        _provider = new JwtTokenProvider(_httpClient, Mock.Of<ILogger<JwtTokenProvider>>(), _options);
    }

    [Fact]
    public void HasValidToken_ShouldReturnFalse_WhenNoTokenSet()
    {
        // Act
        var result = _provider.HasValidToken();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HasValidToken_ShouldReturnTrue_WhenValidTokenSet()
    {
        // Arrange
        var expiresAt = DateTimeOffset.UtcNow.AddHours(1);
        _provider.SetTokens("valid-access-token", "refresh-token", expiresAt);

        // Act
        var result = _provider.HasValidToken();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasValidToken_ShouldReturnFalse_WhenTokenExpired()
    {
        // Arrange
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(-10);
        _provider.SetTokens("expired-access-token", "refresh-token", expiresAt);

        // Act
        var result = _provider.HasValidToken();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HasValidToken_ShouldReturnFalse_WhenTokenExpiringSoon()
    {
        // Arrange - token expires in 3 minutes (within 5-minute buffer)
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(3);
        _provider.SetTokens("soon-to-expire-token", "refresh-token", expiresAt);

        // Act
        var result = _provider.HasValidToken();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ClearTokens_ShouldRemoveAllTokens()
    {
        // Arrange
        _provider.SetTokens("access-token", "refresh-token", DateTimeOffset.UtcNow.AddHours(1));

        // Act
        _provider.ClearTokens();

        // Assert
        _provider.HasValidToken().Should().BeFalse();
    }

    [Fact]
    public async Task GetAccessTokenAsync_ShouldReturnAccessToken_WhenValidTokenExists()
    {
        // Arrange
        _provider.SetTokens("valid-access-token", "refresh-token", DateTimeOffset.UtcNow.AddHours(1));

        // Act
        var token = await _provider.GetAccessTokenAsync();

        // Assert
        token.Should().Be("valid-access-token");
    }

    [Fact]
    public async Task GetAccessTokenAsync_ShouldThrow_WhenNoValidTokenAndNoRefreshToken()
    {
        // Act
        var act = async () => await _provider.GetAccessTokenAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("No valid access token available. Please authenticate first.");
    }

    [Fact]
    public async Task GetAccessTokenAsync_ShouldRefreshToken_WhenAccessTokenExpiredButRefreshTokenExists()
    {
        // Arrange
        _provider.SetTokens("expired-token", "valid-refresh-token", DateTimeOffset.UtcNow.AddMinutes(-10));

        var tokenResponse = new
        {
            AccessToken = "new-access-token",
            RefreshToken = "new-refresh-token",
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        };
        var responseJson = System.Text.Json.JsonSerializer.Serialize(tokenResponse);

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri!.ToString().Contains("/api/v1/auth/refresh")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(new
                {
                    AccessToken = "new-access-token",
                    RefreshToken = "new-refresh-token",
                    ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
                }))
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var provider = new JwtTokenProvider(httpClient, Mock.Of<ILogger<JwtTokenProvider>>(), _options);
        provider.SetTokens("expired-token", "valid-refresh-token", DateTimeOffset.UtcNow.AddMinutes(-10));

        // Act
        var token = await provider.GetAccessTokenAsync();

        // Assert
        token.Should().Be("new-access-token");
    }

    [Fact]
    public async Task GetAccessTokenAsync_ShouldClearTokensAndThrow_WhenRefreshFails()
    {
        // Arrange
        _provider.SetTokens("expired-token", "invalid-refresh-token", DateTimeOffset.UtcNow.AddMinutes(-10));

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri!.ToString().Contains("/api/v1/auth/refresh")),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.Unauthorized
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var provider = new JwtTokenProvider(httpClient, Mock.Of<ILogger<JwtTokenProvider>>(), _options);
        provider.SetTokens("expired-token", "invalid-refresh-token", DateTimeOffset.UtcNow.AddMinutes(-10));

        // Act
        var act = async () => await provider.GetAccessTokenAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        provider.HasValidToken().Should().BeFalse();
    }
}