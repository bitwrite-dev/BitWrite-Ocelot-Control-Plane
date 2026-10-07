using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BitWrite.OcelotControl.SDK;
using BitWrite.OcelotControl.SDK.Authentication;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace BitWrite.OcelotControl.SDK.Tests;

/// <summary>
/// The environment every SDK request names.
/// </summary>
/// <remarks>
/// The control plane stores routes, services and snapshots per environment and rejects
/// a request that names none, so this header is what makes the SDK usable at all
/// rather than a refinement of it.
/// </remarks>
public class OcelotControlClientEnvironmentTests
{
    [Fact]
    public async Task EveryRequestNamesTheConfiguredEnvironment()
    {
        var handler = new RecordingHandler();
        var client = Client(handler, "production");

        await client.GetAsync<string>("/api/v1/routes");

        handler.LastRequest!.Headers.TryGetValues("X-Environment", out var values).Should().BeTrue();
        values.Should().BeEquivalentTo(["production"]);
    }

    [Fact]
    public async Task ARequestThatNamesNoEnvironmentSendsNoHeader()
    {
        // Left absent rather than defaulted: the API's rejection names the header, and a
        // client that quietly filled in "development" would answer with another
        // environment's configuration instead.
        var handler = new RecordingHandler();
        var client = Client(handler, environment: "");

        await client.GetAsync<string>("/api/v1/routes");

        handler.LastRequest!.Headers.Contains("X-Environment").Should().BeFalse();
    }

    private static OcelotControlClient Client(HttpMessageHandler handler, string environment)
    {
        var tokenProvider = new Mock<IJwtTokenProvider>();
        tokenProvider.Setup(p => p.GetAccessTokenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("token-123");

        return new OcelotControlClient(
            new HttpClient(handler),
            tokenProvider.Object,
            Mock.Of<ILogger<OcelotControlClient>>(),
            new OcelotControlClientOptions
            {
                BaseAddress = "https://api.example.com",
                Environment = environment,
            });
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("\"ok\""),
            });
        }
    }
}