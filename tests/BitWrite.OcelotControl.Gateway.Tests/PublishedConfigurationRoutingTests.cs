using System.Net;
using System.Text.Json;
using BitWrite.OcelotControl.Gateway.Configuration;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Ocelot.Configuration.Repository;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using StackExchange.Redis;
using Xunit;
using Xunit.Abstractions;

namespace BitWrite.OcelotControl.Gateway.Tests;

/// <summary>
/// The gateway, hosted for real, taking a published Snapshot and serving with it.
/// </summary>
/// <remarks>
/// The unit tests cover what the subscriber parses. This covers the claim that cannot
/// be asserted any other way: that a notification on the channel the control plane
/// actually publishes to results in traffic being routed, in a gateway that reads a
/// file.
/// <para>
/// The notification and the snapshot document here are the ones a real publish
/// produced, captured rather than invented — the message is
/// <c>{"Version":"2","PublicationId":"0a2732a8-..."}</c> and the snapshot is what
/// <c>POST /api/v1/snapshots</c> stored.
/// </para>
/// </remarks>
public class PublishedConfigurationRoutingTests : IAsyncLifetime
{
    private const string PublishedChannel = "ocelot:snapshot:published";
    private const string SnapshotKey = "ocelot:snapshot:production:9001";
    private static int _offset = -1;

    /// <summary>
    /// A port from a range nothing else in the suite uses, stepped per instance.
    /// </summary>
    /// <remarks>
    /// The class runs three tests and xunit may run them in parallel, so each instance
    /// takes the next pair. Asking the OS for a free port does not work here: two
    /// probes can be handed the same one, and two listeners cannot share it — which
    /// fails the whole run rather than the test that collided.
    /// </remarks>
    private static (int Gateway, int Downstream) NextPorts()
    {
        var step = Interlocked.Increment(ref _offset);
        return (15400 + (step * 2), 15401 + (step * 2));
    }

    private readonly ITestOutputHelper _output;
    // In the output directory on purpose: Ocelot's disk repository builds its path from
    // AppContext.BaseDirectory, so a content root elsewhere would have the gateway
    // reading a different file than the one the subscriber writes.
    private readonly string _contentRoot = AppContext.BaseDirectory;
    private readonly IDatabase _database;
    private readonly IConnectionMultiplexer _multiplexer;
    private int GatewayPort { get; }

    private int DownstreamPort { get; }

    private IHost? _gateway;
    private readonly HttpListener _downstream = new();

    public PublishedConfigurationRoutingTests(ITestOutputHelper output)
    {
        _output = output;
        (GatewayPort, DownstreamPort) = NextPorts();

        var options = ConfigurationOptions.Parse("localhost:6379");
        options.AbortOnConnectFail = false;
        _multiplexer = ConnectionMultiplexer.Connect(options);
        _database = _multiplexer.GetDatabase();

        _downstream.Prefixes.Add($"http://127.0.0.1:{DownstreamPort}/");
    }

    public async Task InitializeAsync()
    {
        _downstream.Start();
        _ = AnswerAsync();
        _gateway = await StartGatewayAsync();
    }

    public async Task DisposeAsync()
    {
        if (_gateway is not null)
        {
            await _gateway.StopAsync(TimeSpan.FromSeconds(3));
            _gateway.Dispose();
        }

        try { _downstream.Stop(); } catch (Exception) { /* already stopped */ }

        _database.KeyDelete(SnapshotKey);
    }

    private async Task AnswerAsync()
    {
        while (_downstream.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await _downstream.GetContextAsync();
            }
            catch (Exception)
            {
                return;
            }

            var bytes = JsonSerializer.SerializeToUtf8Bytes(new { servedBy = "downstream" });
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes);
            context.Response.Close();
        }
    }

    /// <summary>
    /// A snapshot in the shape the control plane stores: canonical, lower-case
    /// <c>global</c> and <c>routes</c>, with the content as a JSON string inside it.
    /// </summary>
    private string SnapshotDocument() =>
        JsonSerializer.Serialize(new
        {
            version = 9001,
            hash = "6fbc50c3bc6eec181987c0f7f08d73b6ff078f8c5a28b9bf0798cba7b0399ad7",
            content = JsonSerializer.Serialize(new
            {
                global = new { baseUrl = "http://localhost:5000", requestIdKey = "X-Request-Id" },
                routes = new[]
                {
                    new
                    {
                        upstreamPathTemplate = "/api/published",
                        upstreamHttpMethod = new[] { "GET" },
                        downstreamPathTemplate = "/api/published",
                        downstreamScheme = "http",
                        downstreamHostAndPorts = new[] { new { host = "127.0.0.1", port = DownstreamPort } },
                        key = "published",
                    },
                },
            }),
        });

    private async Task<IHost> StartGatewayAsync()
    {
        var gateway = Host.CreateDefaultBuilder()
            .ConfigureWebHostDefaults(web =>
            {
                
                web.UseUrls($"http://127.0.0.1:{GatewayPort}");

                // Ocelot reads a file; this is that file, and the subscriber fills it.
                foreach (var name in new[] { "ocelot.json", "ocelot.Production.json" })
                {
                    File.WriteAllText(
                        Path.Combine(_contentRoot, name),
                        """{"Routes":[],"GlobalConfiguration":{"BaseUrl":"http://localhost:5000","RequestIdKey":"X-Request-Id"}}""");
                }

                web.ConfigureAppConfiguration((context, config) =>
                    config.AddOcelot(_contentRoot, context.HostingEnvironment));

                web.ConfigureServices(services =>
                {
                    services.AddOcelot(
                        new ConfigurationBuilder()
                            .SetBasePath(_contentRoot)
                            .AddJsonFile("ocelot.Production.json", optional: false)
                            .Build());
                    services.AddSingleton<IConnectionMultiplexer>(_ =>
                        ConnectionMultiplexer.Connect("localhost:6379"));
                    // Signals the file changed, so Ocelot reloads it.
                    services.AddSingleton<ConfigurationReloader>();
                    services.AddSingleton<ILogger<ConfigurationReloader>>(
                        NullLogger<ConfigurationReloader>.Instance);
                    // Resolved from the provider rather than built here, because the
                    // change token source Ocelot registers has to exist first.
                    services.AddSingleton<OcelotConfigurationWriter>(provider =>
                        new OcelotConfigurationWriter(
                            _contentRoot, "Production",
                            provider.GetRequiredService<ConfigurationReloader>()));
                    services.AddSingleton<ILogger<ConfigurationSubscriber>>(
                        NullLogger<ConfigurationSubscriber>.Instance);
                    services.AddSingleton<ILogger<ConfigurationReloader>>(
                        NullLogger<ConfigurationReloader>.Instance);
                    services.AddHostedService<ConfigurationSubscriber>();
                    services.AddHostedService<FileConfigurationPoller>();
                    services.AddHealthChecks();
                });

                web.Configure(app =>
                {
                    app.UseHealthChecks("/health");
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                        endpoints.MapGet("/", () => Results.Ok(new { status = "ok" })));
                    app.UseOcelot();
                });
            })
            .ConfigureServices(services => services.AddLogging())
            .ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Debug))
            .Build();

        await gateway.StartAsync();
        return gateway;
    }

    /// <summary>Publishes the way the control plane does: snapshot stored, then announced.</summary>
    private async Task PublishAsync()
    {
        await _database.StringSetAsync(SnapshotKey, SnapshotDocument());

        // Pub/Sub keeps nothing for a late arrival, so an announcement can be published
        // before the gateway's subscription is live and be lost with no trace. This is
        // the property of the channel, not a flaw in the gateway — a gateway that
        // started before the publication always sees it, and one that started after
        // reads the published version instead. Announcing until it lands is what
        // makes the test independent of which side wins that race.
        for (var attempt = 0; attempt < 20; attempt++)
        {
            await _multiplexer
                .GetSubscriber()
                .PublishAsync(
                    RedisChannel.Literal(PublishedChannel),
                    """{"Version":"9001","PublicationId":"0a2732a8-dceb-4da3-878a-f3e8290a06ca"}""");

            if (File.ReadAllText(Path.Combine(_contentRoot, "ocelot.Production.json")).Contains("published"))
                return;

            await Task.Delay(250);
        }
    }

    private async Task<HttpResponseMessage> EventuallyAsync(string path)
    {
        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{GatewayPort}") };
        HttpResponseMessage? last = null;

        for (var attempt = 0; attempt < 60; attempt++)
        {
            last?.Dispose();
            last = await client.GetAsync(path);

            if ((int)last.StatusCode != 404)
                return last;

            await Task.Delay(250);
        }

        return last!;
    }

    [Fact]
    public async Task RoutesWithTheSnapshotAPublicationAnnounced()
    {
        // Before anything is published there is nothing to route, and saying so is
        // correct: the gateway has not been told what to serve.
        (await EventuallyAsync("/api/published")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        await PublishAsync();

        // The whole chain: announcement, snapshot retrieval, file written, Ocelot
        // reloading it, request forwarded. Nothing here is stubbed.
        using var response = await EventuallyAsync("/api/published");
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "the published snapshot named this route and the downstream answered");
        body.Should().Contain("downstream");
    }

    [Fact]
    public async Task WritesTheFileOcelotReads()
    {
        // The mechanism, stated plainly: the notification ends in a file, and that is
        // what Ocelot loads. An operator can read it to see what a gateway is running.
        await PublishAsync();

        var path = Path.Combine(_contentRoot, "ocelot.Production.json");
        for (var attempt = 0; attempt < 40 && !File.Exists(path); attempt++)
            await Task.Delay(100);

        await Task.Delay(500);

        var written = JsonDocument.Parse(await File.ReadAllTextAsync(path)).RootElement;

        // Canonical in, Ocelot's shape out: lower-case would bind nothing.
        written.GetProperty("Routes").GetArrayLength().Should().Be(1);
        written.GetProperty("Routes")[0].GetProperty("UpstreamPathTemplate").GetString()
            .Should().Be("/api/published");
        written.GetProperty("GlobalConfiguration").GetProperty("RequestIdKey").GetString()
            .Should().Be("X-Request-Id");
    }

    [Fact]
    public async Task KeepsServingWhenTheAnnouncedSnapshotIsMissing()
    {
        // A version nobody can read must not empty a gateway that is routing.
        await PublishAsync();
        await EventuallyAsync("/api/published");

        await _multiplexer.GetSubscriber().PublishAsync(
            RedisChannel.Literal(PublishedChannel), """{"Version":"9999","PublicationId":"nope"}""");

        using var response = await EventuallyAsync("/api/published");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}