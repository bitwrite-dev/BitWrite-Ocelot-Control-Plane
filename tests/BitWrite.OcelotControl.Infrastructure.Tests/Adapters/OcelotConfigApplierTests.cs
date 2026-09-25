using BitWrite.OcelotControl.Infrastructure.Adapters;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;

namespace BitWrite.OcelotControl.Infrastructure.Tests.Adapters;

public class OcelotConfigApplierTests
{
    private const string PascalCaseConfig = """
    {
        "GlobalConfiguration": { "BaseUrl": "http://localhost:5000" },
        "Routes": [
            {
                "UpstreamPathTemplate": "/api/{everything}",
                "DownstreamHostAndPorts": [ { "Host": "localhost", "Port": 5001 } ]
            }
        ]
    }
    """;

    private const string CamelCaseConfig = """
    {
        "globalConfiguration": { "baseUrl": "http://localhost:5000" },
        "routes": [
            {
                "upstreamPathTemplate": "/api/{everything}",
                "downstreamHostAndPorts": [ { "host": "localhost", "port": 5001 } ]
            }
        ]
    }
    """;

    private readonly Mock<IConfigurationProvider> _provider = new();
    private readonly OcelotConfigApplier _applier;

    public OcelotConfigApplierTests()
    {
        _applier = new OcelotConfigApplier(
            new Mock<ILogger<OcelotConfigApplier>>().Object,
            _provider.Object);
    }

    [Fact]
    public async Task ApplyAsync_ShouldAcceptPascalCaseConfig_OcelotConvention()
    {
        await _applier.ApplyAsync(PascalCaseConfig);

        _provider.Verify(
            p => p.ApplyConfigurationAsync(It.IsAny<JsonElement>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ApplyAsync_ShouldAcceptCamelCaseConfig_DefaultSerializerPolicy()
    {
        await _applier.ApplyAsync(CamelCaseConfig);

        _provider.Verify(
            p => p.ApplyConfigurationAsync(It.IsAny<JsonElement>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ApplyAsync_ShouldRejectConfigWithoutGlobalConfiguration()
    {
        var config = """{ "Routes": [ { "UpstreamPathTemplate": "/a", "DownstreamHostAndPorts": [ { "Host": "h", "Port": 1 } ] } ] }""";

        var act = () => _applier.ApplyAsync(config);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("GlobalConfiguration is required");
    }

    [Fact]
    public async Task ApplyAsync_ShouldRejectConfigWithoutRoutes()
    {
        var config = """{ "GlobalConfiguration": { "BaseUrl": "http://localhost:5000" } }""";

        var act = () => _applier.ApplyAsync(config);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("At least one route is required");
    }

    [Fact]
    public async Task ApplyAsync_ShouldRejectEmptyRoutesArray()
    {
        var config = """{ "GlobalConfiguration": {}, "Routes": [] }""";

        var act = () => _applier.ApplyAsync(config);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("At least one route is required");
    }

    [Fact]
    public async Task ApplyAsync_ShouldRejectRouteWithoutUpstreamPathTemplate()
    {
        var config = """
        {
            "GlobalConfiguration": {},
            "Routes": [ { "DownstreamHostAndPorts": [ { "Host": "h", "Port": 1 } ] } ]
        }
        """;

        var act = () => _applier.ApplyAsync(config);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Route UpstreamPathTemplate is required");
    }

    [Fact]
    public async Task ApplyAsync_ShouldRejectRouteWithBlankUpstreamPathTemplate()
    {
        var config = """
        {
            "GlobalConfiguration": {},
            "Routes": [ { "UpstreamPathTemplate": "   ", "DownstreamHostAndPorts": [ { "Host": "h", "Port": 1 } ] } ]
        }
        """;

        var act = () => _applier.ApplyAsync(config);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Route UpstreamPathTemplate is required");
    }

    [Fact]
    public async Task ApplyAsync_ShouldRejectRouteWithoutDownstreamHosts()
    {
        var config = """
        {
            "GlobalConfiguration": {},
            "Routes": [ { "UpstreamPathTemplate": "/a" } ]
        }
        """;

        var act = () => _applier.ApplyAsync(config);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Route must have at least one downstream host");
    }

    [Fact]
    public async Task ApplyAsync_ShouldRejectRoutesThatAreNotAnArray()
    {
        var config = """{ "GlobalConfiguration": {}, "Routes": { "not": "an array" } }""";

        var act = () => _applier.ApplyAsync(config);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("At least one route is required");
    }

    [Fact]
    public async Task ApplyAsync_ShouldRejectNonObjectRoot()
    {
        var act = () => _applier.ApplyAsync("[]");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("GlobalConfiguration is required");
    }
}
