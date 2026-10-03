using BitWrite.OcelotControl.Gateway.Configuration;
using Microsoft.AspNetCore.Hosting;
using Ocelot.Configuration.Repository;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using StackExchange.Redis;

// Built through ConfigureWebHostDefaults rather than WebApplication.CreateBuilder
// because Ocelot 18's AddOcelot extends IConfigurationBuilder and needs an
// IWebHostEnvironment, and neither is reachable from the minimal hosting model.
var builder = Host.CreateDefaultBuilder(args)
    .ConfigureWebHostDefaults(web =>
    {
        // AddOcelot extends IConfigurationBuilder and needs an IWebHostEnvironment.
        // This callback is the one place that has both. The folder is the content
        // root and the file it reads is ocelot.json there — a real requirement,
        // because Ocelot loads it during startup before the repository below can
        // answer anything. What serves traffic afterwards is the repository.
        web.ConfigureAppConfiguration((context, config) =>
            config.AddOcelot(context.HostingEnvironment.ContentRootPath, context.HostingEnvironment));

        web.ConfigureServices(services =>
        {
            // Runs after the AddOcelot above, so this is the last registration for
            // IFileConfigurationRepository and therefore the one that is used. This
            // is what turns a gateway that reads its own disk into one that reads
            // what the control plane published.
            services.AddSingleton<IFileConfigurationRepository, RedisFileConfigurationRepository>();

            // Ocelot's poller re-reads the repository after startup. Without it a
            // gateway serves whatever configuration existed when it started and
            // ignores every one published since. Ocelot registers the poller's
            // options but not the poller itself.
            services.AddHostedService<FileConfigurationPoller>();
        });

        web.UseStartup<Startup>();
    })
    .Build();

await builder.RunAsync();

/// <summary>Exposed so the tests can host the same pipeline.</summary>
public partial class Program;

/// <summary>
/// The gateway's composition.
/// </summary>
/// <remarks>
/// A gateway that can only be configured by a file on its own disk is a gateway the
/// control plane cannot manage, which is the whole arrangement this project exists to
/// fix: the control plane publishes configuration to Redis and signals a channel, and
/// something has to be listening. This is that something.
/// </remarks>
public class Startup
{
    public Startup(IConfiguration configuration, IWebHostEnvironment environment)
    {
        Configuration = configuration;
        Environment = environment;
    }

    public IConfiguration Configuration { get; }

    public IWebHostEnvironment Environment { get; }

    public void ConfigureServices(IServiceCollection services)
    {
        // Ocelot's services. The AddOcelot in the configuration callback above only
        // reads the file into configuration — it registers nothing, so without this
        // the gateway has no pipeline at all and answers 404 for everything,
        // including paths a published route claims.
        services.AddOcelot(Configuration);

        services.AddSingleton<IConnectionMultiplexer>(_ =>
        {
            var connectionString = Configuration.GetConnectionString("redis")
                ?? throw new InvalidOperationException(
                    "No Redis connection string is configured, so the gateway has nowhere to " +
                    "read its configuration from. Set ConnectionStrings:redis.");

            return ConnectionMultiplexer.Connect(connectionString);
        });

        services.AddCors(options => options.AddDefaultPolicy(policy =>
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

        services.AddHealthChecks();
    }

    public void Configure(IApplicationBuilder app)
    {
        app.UseCors();

        // Ahead of Ocelot, so a gateway that is not routing still reports its own
        // health. A health check that only answers when routing works cannot report
        // that routing is what is broken.
        app.UseHealthChecks("/health");

        // UseRouting and UseEndpoints have to come before Ocelot: Ocelot installs its
        // own endpoint middleware, and it cannot be last in the pipeline when another
        // one is. The gateway's own root route is the only endpoint here — every
        // request that matters is forwarded by Ocelot, not matched here.
        app.UseRouting();
        app.UseEndpoints(endpoints =>
            endpoints.MapGet("/", () => Results.Ok(new { status = "ok" })));

        app.UseOcelot();
    }
}