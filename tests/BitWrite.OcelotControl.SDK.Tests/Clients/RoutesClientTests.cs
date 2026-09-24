using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BitWrite.OcelotControl.SDK;
using BitWrite.OcelotControl.SDK.Clients;
using BitWrite.OcelotControl.SDK.Models.Requests;
using BitWrite.OcelotControl.SDK.Models.Responses;
using FluentAssertions;
using Moq;
using Moq.Protected;
using Xunit;

namespace BitWrite.OcelotControl.SDK.Tests.Clients;

public class RoutesClientTests
{
    private readonly Mock<HttpMessageHandler> _mockHttpHandler;
    private readonly HttpClient _httpClient;
    private readonly Mock<ILogger<OcelotControlClient>> _mockLogger;
    private readonly OcelotControlClientOptions _options;
    private readonly OcelotControlClient _client;
    private readonly RoutesClient _routesClient;

    public RoutesClientTests()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(new Mock<HttpMessageHandler>().Object);
        _mockLogger = new Mock<ILogger<OcelotControlClient>>();
        _options = new OcelotControlClientOptions { BaseAddress = "https://api.example.com" };
        
        _client = new OcelotControlClient(
            new HttpClient(new Mock<HttpMessageHandler>().Object),
            new JwtTokenProvider(new HttpClient(new Mock<HttpMessageHandler>().Object), Mock.Of<ILogger<JwtTokenProvider>>(), new JwtTokenOptions { BaseAddress = "https://api.example.com" }),
            Mock.Of<ILogger<OcelotControlClient>>(),
            _options);
        
        _routesClient = new RoutesClient(_client);
    }

    [Fact]
    public void Constructor_ShouldCreateClient()
    {
        // Act & Assert
        _routesClient.Should().NotBeNull();
    }
}