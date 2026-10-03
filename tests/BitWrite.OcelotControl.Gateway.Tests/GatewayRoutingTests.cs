using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using BitWrite.OcelotControl.Gateway.Configuration;
using Ocelot.Configuration.Creator;
using Ocelot.Configuration.Repository;
using Ocelot.DependencyInjection;
using Ocelot.Logging;
using Ocelot.Middleware;
using StackExchange.Redis;
using Xunit;
using Xunit.Abstractions;

namespace BitWrite.OcelotControl.Gateway.Tests;

/// <summary>
/// The gateway, hosted for real, routing traffic with the configuration the control
/// plane published.
/// </summary>
/// <remarks>
/// The unit tests cover what the repository parses. This covers the thing they cannot:
/// that Ocelot actually loads from it and routes with it. That is the whole claim of
/// the project — without it, a gateway that reads Redis correctly and serves nothing
/// would look finished.
/// </remarks>
public class GatewayRoutingTests : IClassFixture<GatewayRoutingTests.GatewayFixture>, IAsyncLifetime
{
    private const string ConfigurationKey = "ocelot:runtime:config:pending";
    private const int GatewayPort = 5390;
    private const int DownstreamPort = 5391;

    private readonly ITestOutputHelper _output;
    private readonly GatewayFixture _fixture;

    public GatewayRoutingTests(GatewayFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        _fixture.Clear();
        await Task.CompletedTask;
    }

    /// <summary>
    /// Retries until the answer changes, or gives up.
    /// </summary>
    /// <remarks>
    /// The gateway rebuilds its pipeline on a poll, so a request sent immediately
    /// after a publish can be answered by the pipeline built before it. Waiting for
    /// the outcome rather than for a fixed delay keeps the test about the behaviour
    /// and off the machine's speed.
    /// </remarks>
    private static async Task<HttpResponseMessage> EventuallyAsync(
        Func<Task<HttpResponseMessage>> request,
        int attempts = 40)
    {
        HttpResponseMessage? last = null;

        for (var attempt = 0; attempt < attempts; attempt++)
        {
            last?.Dispose();
            last = await request();

            if (last.StatusCode != HttpStatusCode.NotFound)
                return last;

            await Task.Delay(250);
        }

        return last!;
    }

    private static string RouteTo(int downstreamPort) =>
        $$"""
        {
          "Routes": [
            {
              "UpstreamPathTemplate": "/api/probe",
              "UpstreamHttpMethod": ["GET"],
              "DownstreamPathTemplate": "/api/probe",
              "DownstreamScheme": "http",
              "DownstreamHostAndPorts": [{ "Host": "127.0.0.1", "Port": {{downstreamPort}} }],
              "Key": "probe"
            }
          ],
          "GlobalConfiguration": { "RequestIdKey": "X-Request-Id" }
        }
        """;

    [Fact]
    public async Task ProxiesARequestToWhereTheControlPlanePointedIt()
    {
        // The test this project exists for: configuration published to Redis, and the
        // gateway forwarding to that downstream.
        _fixture.Publish(RouteTo(DownstreamPort));

        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{GatewayPort}") };
        var response = await EventuallyAsync(() => client.GetAsync("/api/probe"));

        var body = await response.Content.ReadAsStringAsync();
        _output.WriteLine($"status={(int)response.StatusCode} body={body}");

        response.StatusCode.Should().Be(HttpStatusCode.OK, "the downstream answered and the gateway forwarded to it");
        body.Should().Contain("downstream");
    }

    [Fact]
    public async Task AnswersForAHealthCheckEvenWithNoConfigurationPublished()
    {
        // Otherwise there is nothing running to report that routing is what is broken.
        _fixture.Clear();

        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{GatewayPort}") };
        using var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ReportsNotFoundForAPathNoRouteClaims()
    {
        _fixture.Publish(RouteTo(DownstreamPort));

        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{GatewayPort}") };
        using var response = await client.GetAsync("/nothing-here");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// A real gateway process and a real downstream, both on loopback.
    /// </summary>
    /// <remarks>
    /// Not an in-memory test host: Ocelot's pipeline and configuration loading are
    /// what is being checked, and neither runs the way it does under
    /// <c>WebApplicationFactory</c>.
    /// </remarks>
    public sealed class GatewayFixture : IAsyncLifetime
    {
        private readonly IDatabase _database;
        private readonly List<IAsyncDisposable> _hosts = [];

        public GatewayFixture()
        {
            var configuration = ConfigurationOptions.Parse("localhost:6379");
            configuration.AbortOnConnectFail = false;
            _database = ConnectionMultiplexer.Connect(configuration).GetDatabase();
        }

        public IDatabase Database => _database;

        public void Clear() => _database.KeyDelete(ConfigurationKey);

        public void Publish(string configuration) =>
            _database.StringSet(ConfigurationKey, configuration);

        public async Task InitializeAsync()
        {
            _hosts.Add(await GatewayHost.StartAsync(GatewayPort, "localhost:6379"));
            _hosts.Add(await Downstream.StartAsync(DownstreamPort));
        }

        public async Task DisposeAsync()
        {
            foreach (var host in _hosts)
                await host.DisposeAsync();
        }
    }

    /// <summary>A downstream that answers, so a proxied request can be told apart from a gateway response.</summary>
    private sealed class Downstream : IAsyncDisposable
    {
        private readonly HttpListener _listener = new();

        private Downstream(int port) => _listener.Prefixes.Add($"http://127.0.0.1:{port}/");

        public static Task<Downstream> StartAsync(int port)
        {
            var downstream = new Downstream(port);
            downstream._listener.Start();
            _ = downstream.LoopAsync();
            return Task.FromResult(downstream);
        }

        private async Task LoopAsync()
        {
            while (_listener.IsListening)
            {
                HttpListenerContext context;
                try
                {
                    context = await _listener.GetContextAsync();
                }
                catch (Exception)
                {
                    return;
                }

                var body = "downstream reached";
                context.Response.ContentType = "application/json";
                var bytes = System.Text.Encoding.UTF8.GetBytes($"{{\"servedBy\":\"{body}\"}}");
                context.Response.ContentLength64 = bytes.Length;
                await context.Response.OutputStream.WriteAsync(bytes);
                context.Response.Close();
            }
        }

        public ValueTask DisposeAsync()
        {
            try { _listener.Stop(); } catch (Exception) { /* already stopped */ }
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>The gateway, started the way a deployment would start it.</summary>
    private sealed class GatewayHost : IAsyncDisposable
    {
        private readonly IHost _host;

        private GatewayHost(IHost host) => _host = host;

        public static async Task<GatewayHost> StartAsync(int port, string redis)
        {
            var host = Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(web =>
                {
                    web.UseUrls($"http://127.0.0.1:{port}");
                    web.UseSetting("ConnectionStrings:redis", redis);
                    web.ConfigureAppConfiguration((_, config) =>
                        config.AddInMemoryCollection(new Dictionary<string, string?>
                        {
                            ["ConnectionStrings:redis"] = redis,
                        }));
                    web.ConfigureAppConfiguration(
                        (context, config) => config.AddOcelot(context.HostingEnvironment.ContentRootPath, context.HostingEnvironment));
                    web.ConfigureServices(services =>
                    {
                        services.AddOcelot(new ConfigurationBuilder().Build());
                        services.AddHealthChecks();
                        services.AddSingleton<IConnectionMultiplexer>(_ =>
                            ConnectionMultiplexer.Connect(redis));
                        services.AddSingleton<IFileConfigurationRepository, RedisFileConfigurationRepository>();
                        services.AddHostedService<FileConfigurationPoller>();
                    });
                    // AddOcelot registers its own file-backed repository; ours has to
                    // come after it or the last registration wins and the gateway
                    // reads a file nobody writes.
                    web.Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapHealthChecks("/health");
                            endpoints.MapGet("/", () => Results.Ok(new { status = "ok" }));
                        });
                        app.UseOcelot();
                    });
                })
                .Build();

            await host.StartAsync();
            return new GatewayHost(host);
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await _host.StopAsync(cancellation.Token);
            }
            catch (Exception)
            {
                // Already stopped, or never started; either way there is nothing to do.
            }

            _host.Dispose();
        }
    }
}