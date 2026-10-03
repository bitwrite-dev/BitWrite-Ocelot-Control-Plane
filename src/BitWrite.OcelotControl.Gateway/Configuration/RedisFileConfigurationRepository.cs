using System.Text.Json;
using Ocelot.Configuration.File;
using Ocelot.Configuration.Repository;
using Ocelot.Configuration.Validator;
using Ocelot.Requester;
using Ocelot.Responses;
using StackExchange.Redis;

namespace BitWrite.OcelotControl.Gateway.Configuration;

/// <summary>
/// Reads the gateway's configuration out of Redis, where the control plane puts it.
/// </summary>
/// <remarks>
/// Ocelot 18 can only be pointed at a file — there is no configuration-provider
/// interface in this version — so the seam it does offer is
/// <see cref="IFileConfigurationRepository"/>, which every Ocelot file load goes
/// through. Replacing it is what lets a gateway take configuration pushed to it
/// rather than only what was on disk when it started.
/// <para>
/// The control plane publishes to <c>ocelot:runtime:config:pending</c> and signals
/// <c>ocelot:config:update</c>. This reads the same key Ocelot's own
/// <c>FileConfigurationPoller</c> re-reads on a timer, so a change lands within one
/// poll rather than needing a restart, and the key stays the single place either
/// side looks.
/// </para>
/// <para>
/// A Redis failure is reported as a failed <see cref="Response"/>, not thrown. Ocelot
/// asks for configuration on the request path, and a gateway that throws while
/// building its pipeline answers every request with a 500 — so a gateway that has
/// lost Redis keeps serving the routes it already has and says so, instead of taking
/// traffic down with it.
/// </para>
/// </remarks>
public sealed class RedisFileConfigurationRepository : IFileConfigurationRepository
{
    private const string ConfigurationKey = "ocelot:runtime:config:pending";

    private readonly IDatabase _database;
    private readonly ILogger<RedisFileConfigurationRepository> _logger;

    public RedisFileConfigurationRepository(
        IConnectionMultiplexer connectionMultiplexer,
        ILogger<RedisFileConfigurationRepository> logger)
    {
        _database = connectionMultiplexer.GetDatabase();
        _logger = logger;
    }

    public async Task<Response<FileConfiguration>> Get()
    {
        try
        {
            var json = await _database.StringGetAsync(ConfigurationKey);

            if (json.IsNullOrEmpty)
            {
                // Nothing published yet. An empty configuration is a valid answer and
                // keeps the gateway serving 404s for unrouted paths, rather than
                // failing to start and refusing every request including its health
                // check — which would leave nothing to report the problem with.
                _logger.LogInformation(
                    "No configuration published at {Key}; starting with no routes", ConfigurationKey);

                return new OkResponse<FileConfiguration>(new FileConfiguration());
            }

            var configuration = JsonSerializer.Deserialize<FileConfiguration>(
                json.ToString(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (configuration is null)
            {
                _logger.LogError(
                    "Published configuration at {Key} is not an Ocelot configuration; keeping the routes already loaded",
                    ConfigurationKey);

                return new ErrorResponse<FileConfiguration>(
                    new FileValidationFailedError("Published configuration could not be read"));
            }

            return new OkResponse<FileConfiguration>(configuration);
        }
        catch (JsonException ex)
        {
            _logger.LogError(
                ex, "Published configuration at {Key} is not valid JSON; keeping the routes already loaded",
                ConfigurationKey);

            return new ErrorResponse<FileConfiguration>(
                new FileValidationFailedError("Published configuration could not be read"));
        }
        catch (Exception ex) when (ex is RedisConnectionException or RedisTimeoutException)
        {
            // Not fatal: the caller already holds a working configuration, and
            // replacing it with nothing because Redis blinked would drop every route.
            _logger.LogWarning(
                ex, "Could not read configuration from {Key}; keeping the routes already loaded",
                ConfigurationKey);

            return new ErrorResponse<FileConfiguration>(
                new UnableToCompleteRequestError(
                    new InvalidOperationException("Configuration store is unavailable")));
        }
    }

    public async Task<Response> Set(FileConfiguration fileConfiguration)
    {
        try
        {
            var json = JsonSerializer.Serialize(
                fileConfiguration,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            await _database.StringSetAsync(ConfigurationKey, json);

            return new OkResponse();
        }
        catch (Exception ex) when (ex is RedisConnectionException or RedisTimeoutException)
        {
            _logger.LogError(ex, "Could not write configuration to {Key}", ConfigurationKey);

            return new ErrorResponse(
                new UnableToCompleteRequestError(
                    new InvalidOperationException("Configuration store is unavailable")));
        }
    }
}