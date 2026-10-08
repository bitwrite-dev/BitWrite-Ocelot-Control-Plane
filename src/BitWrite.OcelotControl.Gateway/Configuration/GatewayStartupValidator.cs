using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;

namespace BitWrite.OcelotControl.Gateway.Configuration;

/// <summary>
/// Validates that the published configuration matches this gateway's environment at startup.
/// </summary>
/// <remarks>
/// Runs during host startup before the Ocelot pipeline is built. Reads the
/// <c>ocelot:runtime:current:{env}</c> key from Redis, extracts the environment
/// from the stored JSON, and compares it with this gateway's configured environment.
/// A mismatch means this gateway would serve another environment's configuration,
/// which is the cross-environment leak environment isolation exists to prevent.
/// The gateway refuses to start (throws) rather than serving wrong configuration.
/// </remarks>
internal sealed class GatewayStartupValidator
{
    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly ILogger<GatewayStartupValidator> _logger;

    public GatewayStartupValidator(
        IConnectionMultiplexer connectionMultiplexer,
        ILogger<GatewayStartupValidator> logger)
    {
        _connectionMultiplexer = connectionMultiplexer;
        _logger = logger;
    }

    /// <summary>
    /// Validates that the current published version matches this gateway's environment.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when environment mismatch (403 equivalent).</exception>
    public async Task ValidateAsync(CancellationToken cancellationToken = default)
    {
        var db = _connectionMultiplexer.GetDatabase();

        var currentKey = PublishedEnvironment.RuntimeCurrentKey();
        var current = await db.StringGetAsync(currentKey);

        if (current.IsNullOrEmpty)
        {
            _logger.LogInformation(
                "No published version recorded for environment {Environment}; gateway will wait for publication",
                PublishedEnvironment.KeySegment);
            return;
        }

        var message = current.ToString();
        var (version, environment) = ParseCurrentMessage(message);

        if (version is null)
        {
            _logger.LogWarning("Could not parse version from current key; gateway will wait for publication");
            return;
        }

        if (!PublishedEnvironment.Matches(environment))
        {
            _logger.LogError(
                "Environment mismatch at startup: current published version is for environment {Env} but this gateway serves {Served}. Refusing to start (403).",
                environment, PublishedEnvironment.KeySegment);

            throw new InvalidOperationException(
                $"Gateway environment mismatch: current published version is for environment '{environment}' but this gateway is configured for '{PublishedEnvironment.KeySegment}'. " +
                "This gateway would serve the wrong environment's configuration. " +
                "Ensure the gateway and control plane are configured for the same environment.");
        }

        _logger.LogInformation(
            "Startup validation passed: current version {Version} is for environment {Environment}",
            version, PublishedEnvironment.KeySegment);
    }

    private static (string? Version, string? Environment) ParseCurrentMessage(string message)
    {
        try
        {
            using var parsed = JsonDocument.Parse(message);
            var root = parsed.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
                return (null, null);

            var version = ReadProperty(root, "Version", "version");
            var environment = ReadProperty(root, "Environment", "environment");

            return (version, environment);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }

    private static string? ReadProperty(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var value))
                continue;

            var text = value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : value.ToString();

            if (!string.IsNullOrWhiteSpace(text))
                return text.Trim();
        }

        return null;
    }
}