using System.Text.Json;
using BitWrite.OcelotControl.Gateway.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Ocelot.Responses;
using StackExchange.Redis;
using Xunit;

namespace BitWrite.OcelotControl.Gateway.Tests.Configuration;

/// <summary>
/// The gateway reading the configuration the control plane publishes.
/// </summary>
/// <remarks>
/// This is the whole point of the project. Ocelot 18 can only read a file, so without
/// this repository a gateway is configured by whatever happened to be on its disk when
/// it started, and the control plane's published configuration is read by nobody.
/// </remarks>
public class RedisFileConfigurationRepositoryTests
{
    private const string ConfigurationKey = "ocelot:runtime:config:pending";

    private readonly Mock<IDatabase> _db = new();
    private readonly Dictionary<string, RedisValue> _stored = new(StringComparer.Ordinal);
    private readonly RedisFileConfigurationRepository _repository;

    public RedisFileConfigurationRepositoryTests()
    {
        var multiplexer = new Mock<IConnectionMultiplexer>();
        multiplexer.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_db.Object);

        _db.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .Returns<RedisKey, CommandFlags>((key, _) =>
                Task.FromResult(_stored.TryGetValue(key.ToString()!, out var value)
                    ? value
                    : RedisValue.Null));

        _db.Setup(d => d.StringSetAsync(
                It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
            .Returns<RedisKey, RedisValue, TimeSpan?, bool, When, CommandFlags>(
                (key, value, _, _, _, _) =>
                {
                    _stored[key.ToString()!] = value;
                    return Task.FromResult(true);
                });

        _repository = new RedisFileConfigurationRepository(
            multiplexer.Object,
            Mock.Of<ILogger<RedisFileConfigurationRepository>>());
    }

    private void Published(string json) =>
        _stored[ConfigurationKey] = json;

    /// <summary>
    /// A route shaped the way ConfigurationBuilder emits it, since that is what will
    /// actually arrive at this key.
    /// </summary>
    private static string PublishedConfiguration(string downstream = "http://orders:8080") =>
        $$"""
        {
          "Routes": [
            {
              "UpstreamPathTemplate": "/api/orders",
              "UpstreamHttpMethod": ["GET", "POST"],
              "DownstreamPathTemplate": "/api/orders",
              "DownstreamScheme": "http",
              "DownstreamHostAndPorts": [{ "Host": "orders", "Port": 8080 }],
              "Key": "orders"
            }
          ],
          "GlobalConfiguration": {
            "BaseUrl": "http://localhost:5000",
            "RequestIdKey": "X-Request-Id"
          }
        }
        """;

    [Fact]
    public async Task ReadsTheConfigurationTheControlPlanePublished()
    {
        Published(PublishedConfiguration());

        var response = await _repository.Get();

        response.Data.Should().NotBeNull();
        response.Data!.Routes.Should().ContainSingle();
        response.Data.Routes[0].UpstreamPathTemplate.Should().Be("/api/orders");
        response.Data.GlobalConfiguration.Should().NotBeNull();
        response.Data.GlobalConfiguration.RequestIdKey.Should().Be("X-Request-Id");
    }

    [Fact]
    public async Task ReadsTheRoutesItWasGivenRatherThanAFileOnDisk()
    {
        // The regression this project exists to prevent: a gateway that ignores what
        // the control plane published because it was pointed at a file instead.
        Published(PublishedConfiguration());

        var response = await _repository.Get();

        response.Data!.Routes[0].DownstreamHostAndPorts!.First().Host.Should().Be("orders");
    }

    [Fact]
    public async Task ReportsNoRoutesRatherThanFailingWhenNothingHasBeenPublished()
    {
        // A gateway starting before anything is published must still come up: an
        // error here would leave no process to report the problem, and no health
        // check to diagnose it with.
        var response = await _repository.Get();

        response.Data.Should().NotBeNull();
        response.Data!.Routes.Should().BeEmpty();
        response.IsError.Should().BeFalse();
    }

    [Fact]
    public async Task ReportsAnErrorRatherThanThrowingWhenThePublishedDocumentIsUnreadable()
    {
        Published("{ this is not json");

        var response = await _repository.Get();

        response.IsError.Should().BeTrue();
        response.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ReportsAnErrorRatherThanThrowingWhenRedisIsUnreachable()
    {
        // Ocelot asks for configuration on the request path. Throwing here would
        // answer every request with a 500 — so a gateway that has lost Redis keeps
        // serving the routes it already has.
        _db.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "no"));

        var response = await _repository.Get();

        response.IsError.Should().BeTrue();
    }

    [Fact]
    public async Task RoundTripsAConfigurationItWrote()
    {
        var original = await ReadBack(PublishedConfiguration());

        await _repository.Set(original);

        var reread = await _repository.Get();

        reread.Data!.Routes.Should().ContainSingle();
        reread.Data.Routes[0].UpstreamPathTemplate.Should().Be("/api/orders");
        reread.Data.Routes[0].UpstreamHttpMethod.Should().Contain("POST");
    }

    [Fact]
    public async Task WritesToTheKeyTheControlPlanePublishesTo()
    {
        await _repository.Set(await ReadBack(PublishedConfiguration()));

        // Both sides must name one key, or the gateway reads what nobody wrote.
        _stored.Should().ContainKey(ConfigurationKey);
    }

    /// <summary>Parses a document the way the control plane's builder emits it.</summary>
    private static async Task<Ocelot.Configuration.File.FileConfiguration> ReadBack(string json) =>
        (await JsonSerializer.DeserializeAsync<Ocelot.Configuration.File.FileConfiguration>(
            new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json))))!;
}