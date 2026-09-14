using BitWrite.OcelotControl.Application.Interfaces;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;

namespace BitWrite.OcelotControl.Infrastructure.Adapters;

/// <summary>
/// Implements IOcelotConfigApplier to apply Ocelot configuration dynamically.
/// Uses Ocelot's dynamic configuration capabilities for graceful reload.
/// </summary>
public class OcelotConfigApplier : IOcelotConfigApplier
{
    private readonly ILogger<OcelotConfigApplier> _logger;
    private readonly IConfigurationProvider _configurationProvider;

    public OcelotConfigApplier(
        ILogger<OcelotConfigApplier> logger,
        IConfigurationProvider configurationProvider)
    {
        _logger = logger;
        _configurationProvider = configurationProvider;
    }

    public async Task ApplyAsync(string configuration, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Applying Ocelot configuration (length: {Length} chars)", configuration.Length);

        try
        {
            // 1. Parse and validate the Ocelot JSON configuration
            var fileConfiguration = ParseConfiguration(configuration);
            ValidateConfiguration(fileConfiguration);

            // 2. Apply configuration dynamically via Ocelot's configuration provider
            await _configurationProvider.ApplyConfigurationAsync(fileConfiguration, cancellationToken);

            _logger.LogInformation("Ocelot configuration applied successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply Ocelot configuration");
            throw;
        }
    }

    private JsonElement ParseConfiguration(string json)
    {
        var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    private void ValidateConfiguration(JsonElement configuration)
    {
        if (!configuration.TryGetProperty("GlobalConfiguration", out _))
        {
            throw new InvalidOperationException("GlobalConfiguration is required");
        }

        if (!configuration.TryGetProperty("Routes", out var routes) || routes.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("At least one route is required");
        }

        foreach (var route in routes.EnumerateArray())
        {
            if (!route.TryGetProperty("UpstreamPathTemplate", out var path) || string.IsNullOrWhiteSpace(path.GetString()))
            {
                throw new InvalidOperationException("Route UpstreamPathTemplate is required");
            }

            if (!route.TryGetProperty("DownstreamHostAndPorts", out var hosts) || hosts.GetArrayLength() == 0)
            {
                throw new InvalidOperationException("Route must have at least one downstream host");
            }
        }
    }
}

/// <summary>
/// Interface for Ocelot configuration provider (can be implemented for different deployment scenarios)
/// </summary>
public interface IConfigurationProvider
{
    Task ApplyConfigurationAsync(JsonElement configuration, CancellationToken cancellationToken = default);
}

/// <summary>
/// Redis-backed configuration provider for distributed environments
/// </summary>
public class RedisConfigurationProvider : IConfigurationProvider
{
    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly ILogger<RedisConfigurationProvider> _logger;
    private const string ConfigKey = "ocelot:runtime:config:pending";
    private const string NotificationChannel = "ocelot:config:update";

    public RedisConfigurationProvider(
        IConnectionMultiplexer connectionMultiplexer,
        ILogger<RedisConfigurationProvider> logger)
    {
        _connectionMultiplexer = connectionMultiplexer;
        _logger = logger;
    }

public async Task ApplyConfigurationAsync(JsonElement configuration, CancellationToken cancellationToken = default)
    {
        var db = _connectionMultiplexer.GetDatabase();
        var subscriber = _connectionMultiplexer.GetSubscriber();

        // Serialize configuration
        var json = JsonSerializer.Serialize(configuration, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // Store configuration in Redis
        await db.StringSetAsync(ConfigKey, json);

        // Publish notification to trigger reload
        await subscriber.PublishAsync(NotificationChannel, json);

        _logger.LogInformation("Ocelot configuration published to Redis, notification sent to channel {Channel}", NotificationChannel);
    }
}

/// <summary>
/// File-based configuration provider for single-instance deployments
/// </summary>
public class FileConfigurationProvider : IConfigurationProvider
{
    private readonly string _configFilePath;
    private readonly ILogger<FileConfigurationProvider> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public FileConfigurationProvider(string configFilePath, ILogger<FileConfigurationProvider> logger)
    {
        _configFilePath = configFilePath;
        _logger = logger;
    }

    public async Task ApplyConfigurationAsync(JsonElement configuration, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var json = JsonSerializer.Serialize(configuration, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });

            await File.WriteAllTextAsync(_configFilePath, JsonSerializer.Serialize(configuration, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            }), cancellationToken);
            
            _logger.LogInformation("Ocelot configuration written to file");
        }
        finally
        {
            _lock.Release();
        }
    }
}