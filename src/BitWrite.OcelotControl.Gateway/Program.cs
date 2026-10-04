using BitWrite.OcelotControl.Gateway.Configuration;
using Microsoft.AspNetCore.Hosting;
using Ocelot.Configuration.Repository;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using StackExchange.Redis;

// Built through ConfigureWebHostDefaults rather than WebApplication.CreateBuilder,
// because Ocelot 18's AddOcelot extends IConfigurationBuilder and needs an
// IWebHostEnvironment, and neither is reachable from the minimal hosting model.
// The file Ocelot reads and the environment the published snapshot is stored under
// are the same setting, so both come from one place and cannot drift.
const string EnvironmentConfigurationFile = PublishedEnvironment.ConfigurationFile;
string _contentRoot = AppContext.BaseDirectory;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureWebHostDefaults(web =>
    {


        // The content root the host resolved, captured so the file registration below
        // reads the same directory Ocelot will.
        var contentRoot = System.IO.Directory.GetCurrentDirectory();

        // Ocelot resolves its configuration file from the content root, so this has to
        // be set before the callback below reads it — and it is itself a configuration
        // callback, so it comes first in the chain. The assembly's own directory, rather
        // than the working directory: that is the project folder when launched from
        // there and the output folder when deployed, and only the assembly's directory
        // is the same place in both.
        // Ocelot reads a file. This is that file, and it is a real requirement: Ocelot
        // loads it during startup, before the subscriber can act on a publication.
        // What fills it afterwards is the subscriber.
        web.ConfigureAppConfiguration((context, config) =>
            config.AddOcelot(context.HostingEnvironment.ContentRootPath, context.HostingEnvironment));

        web.ConfigureServices(services =>
        {
            // Ocelot's own services. The AddOcelot above only reads the file into
            // configuration — it registers nothing, so without this the gateway has no
            // pipeline at all and answers 404 for every path, including ones a
            // published route claims.
            //
            // Given the built configuration rather than the host's, because this
            // registration is what turns the file the callback above read into a
            // pipeline that serves it.
            services.AddOcelot(
                new ConfigurationBuilder()
                    .SetBasePath(contentRoot)
                    .AddJsonFile(EnvironmentConfigurationFile, optional: false)
                    .Build());

            services.AddSingleton<IConnectionMultiplexer>(provider =>
            {
                var connectionString = provider
                    .GetRequiredService<IConfiguration>()
                    .GetConnectionString("redis")
                    ?? throw new InvalidOperationException(
                        "No Redis connection string is configured, so the gateway has nowhere to " +
                        "learn what was published. Set ConnectionStrings:redis.");

                return ConnectionMultiplexer.Connect(connectionString);
            });

            // Signals the file changed, so Ocelot reloads it rather than serving
            // whatever it read at startup.
            services.AddSingleton<ConfigurationReloader>();

            // Applies the signal. Ocelot registers the change token and the internal
            // repository but not the poller that reads the token and rebuilds the
            // pipeline, so a publication would be written to disk and then ignored.
            services.AddHostedService<FileConfigurationPoller>();

            // Writes the file Ocelot reads, then signals. Named after the same
            // environment the snapshot is read from rather than after
            // ASPNETCORE_ENVIRONMENT: those two names have to agree, and the host's is
            // whatever the container happened to be started with.
            services.AddSingleton(serviceProvider =>
            {
                var environment = serviceProvider.GetRequiredService<IHostEnvironment>();

                return new OcelotConfigurationWriter(
                    environment.ContentRootPath,
                    PublishedEnvironment.FileBaseName,
                    serviceProvider.GetRequiredService<ConfigurationReloader>());
            });

            // Subscribes to ocelot:snapshot:published and loads the snapshot it names.
            // §19 and §20.3: the notification carries a version, not the configuration.
            services.AddHostedService<ConfigurationSubscriber>();

            services.AddHealthChecks();
        });

        web.Configure(app =>
        {
            // Ahead of Ocelot, so a gateway that is not routing still reports its own
            // health. A health check that only answers when routing works cannot report
            // that routing is what is broken.
            app.UseHealthChecks("/health");

            // Ahead of Ocelot because Ocelot installs its own endpoint middleware and
            // cannot be last in the pipeline when another one is.
            app.UseRouting();
            app.UseEndpoints(endpoints =>
                endpoints.MapGet("/", () => Results.Ok(new { status = "ok" })));

            app.UseOcelot();
        });
    })
    .Build();

await host.RunAsync();

/// <summary>Exposed so the tests can host the same pipeline.</summary>
public partial class Program;