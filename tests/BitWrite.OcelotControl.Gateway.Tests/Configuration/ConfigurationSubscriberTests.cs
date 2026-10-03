using System.Text.Json;
using BitWrite.OcelotControl.Gateway.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using Xunit;
using Xunit.Abstractions;

namespace BitWrite.OcelotControl.Gateway.Tests.Configuration;

/// <summary>
/// The gateway turning a publication notification into an ocelot.json it can serve.
/// </summary>
/// <remarks>
/// The spec is explicit about the order (§19, §20.3): the Gateway consumes a
/// published Snapshot, and Pub/Sub is a notification, not the source of truth — it
/// carries a version and nothing else. So the notification is not the configuration,
/// and treating it as one would mean serving whatever a message happened to contain
/// rather than what was published.
/// </remarks>
public class ConfigurationSubscriberTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("gateway-config-").FullName;
    private readonly Mock<IDatabase> _db = new();
    private readonly ITestOutputHelper _output;

    public ConfigurationSubscriberTests(ITestOutputHelper output)
    {
        _output = output;

        var stored = new Dictionary<string, RedisValue>(StringComparer.Ordinal);

        var multiplexer = new Mock<IConnectionMultiplexer>();
        multiplexer.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_db.Object);

        Subscriber = new ConfigurationSubscriber(
            multiplexer.Object,
            new OcelotConfigurationWriter(_directory, "Production"),
            Mock.Of<ILogger<ConfigurationSubscriber>>());
    }

    private ConfigurationSubscriber Subscriber { get; }

    private string ConfigPath => Path.Combine(_directory, "ocelot.Production.json");

    /// <summary>
    /// A notification exactly as the control plane publishes it, captured from a real
    /// publish rather than invented here.
    /// </summary>
    private static string Notification(string version) =>
        $$"""{"Version":"{{version}}","PublicationId":"0a2732a8-dceb-4da3-878a-f3e8290a06ca"}""";

    /// <summary>A snapshot exactly as CreateSnapshot stores it, captured from the API.</summary>
    private static string SnapshotDocument(string version, string content) =>
        $$"""
        {"version":{{version}},"hash":"fcc16f2f1dd6e0e2ff0361df126677c59d567bf85c87fe619fb1c1db0ee3efc1",
         "content":{{JsonSerializer.Serialize(content)}}}
        """;

    public void Dispose() => Directory.Delete(_directory, true);

    [Fact]
    public async Task RetrievesTheSnapshotTheNotificationNamed()
    {
        // The notification carries a version and nothing else, so the configuration
        // has to come from the immutable snapshot. Treating the message as the
        // configuration would serve whatever a message happened to contain.
        var content = """{"global":{"baseUrl":"http://gw:5000","requestIdKey":"X-Request-Id"},"routes":[]}""";
        StoredSnapshot("2", content);

        await Subscriber.HandleAsync(Notification("2"), CancellationToken.None);

        File.Exists(ConfigPath).Should().BeTrue();
        File.ReadAllText(ConfigPath).Should().Contain("http://gw:5000");
    }

    [Fact]
    public async Task WritesTheSnapshotAsOcelotUnderstandsIt()
    {
        // A snapshot holds the canonical form — lower-case `global` and `routes` —
        // while Ocelot reads `GlobalConfiguration` and `Routes`. Written straight
        // through, Ocelot would find no routes and answer 404 for everything.
        var content = """
        {"global":{"baseUrl":"http://gw:5000","requestIdKey":"X-Request-Id"},
         "routes":[{"upstreamPathTemplate":"/api/orders","upstreamHttpMethod":["GET"],
                    "downstreamPathTemplate":"/orders","downstreamScheme":"http",
                    "downstreamHostAndPorts":[{"host":"orders","port":8080}],"key":"orders"}]}
        """;
        StoredSnapshot("2", content);

        await Subscriber.HandleAsync(Notification("2"), CancellationToken.None);

        var written = JsonDocument.Parse(File.ReadAllText(ConfigPath)).RootElement;
        written.GetProperty("Routes").GetArrayLength().Should().Be(1);
        written.GetProperty("GlobalConfiguration").GetProperty("BaseUrl").GetString()
            .Should().Be("http://gw:5000");
    }

    [Fact]
    public async Task LeavesTheFileItAlreadyHadWhenTheSnapshotIsGone()
    {
        // A notification for a snapshot nobody can read must not empty a gateway that
        // is serving traffic. The last known-good configuration is better than none.
        await Subscriber.HandleAsync(Notification("2"), CancellationToken.None);
        await File.WriteAllTextAsync(ConfigPath, """{"Routes":[{"Key":"serving"}]}""");

        await Subscriber.HandleAsync(Notification("99"), CancellationToken.None);

        File.ReadAllText(ConfigPath).Should().Contain("serving");
    }

    [Fact]
    public async Task RefusesASnapshotWhoseContentIsNotAConfiguration()
    {
        await Subscriber.HandleAsync(Notification("2"), CancellationToken.None);

        await Subscriber.HandleAsync("""{"Version":"","PublicationId":""}""",
            CancellationToken.None);

        File.Exists(ConfigPath).Should().BeFalse();
    }

    [Fact]
    public async Task ReportsRatherThanThrowsWhenTheStoreIsUnreachable()
    {
        // The subscription runs on a background timer; an exception here would take the
        // gateway down rather than leave it serving what it has.
        _db.Setup(d => d.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ThrowsAsync(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "no"));

        var act = () => Subscriber.HandleAsync(Notification("2"), CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task LeavesAReadableFileBehind()
    {
        // Whatever else is true of the write, what it leaves is complete JSON — a
        // half-written file would read as a configuration with no routes.
        var content = """{"global":{"baseUrl":"http://gw:5000","requestIdKey":"X-Request-Id"},"routes":[]}""";
        StoredSnapshot("2", content);

        await Subscriber.HandleAsync(Notification("2"), CancellationToken.None);

        JsonDocument.Parse(File.ReadAllText(ConfigPath)).Dispose();
    }

    [Fact]
    public async Task SaysWhichVersionItLoaded()
    {
        // The loaded version is what an operator needs to tell a gateway apart from
        // one that never received the publication.
        var content = """{"global":{"baseUrl":"http://gw:5000","requestIdKey":"X-Request-Id"},"routes":[]}""";
        StoredSnapshot("2", content);

        await Subscriber.HandleAsync(Notification("2"), CancellationToken.None);

        File.ReadAllText(ConfigPath).Should().Contain("http://gw:5000");
    }

    private void StoredSnapshot(string version, string content)
    {
        _db.Setup(d => d.StringGetAsync($"ocelot:snapshot:{version}", It.IsAny<CommandFlags>()))
            .ReturnsAsync(SnapshotDocument(version, content));
    }
}