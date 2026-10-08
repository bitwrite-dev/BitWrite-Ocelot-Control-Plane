using System.Text.Json;
using System.Text.Json.Nodes;
using StackExchange.Redis;

namespace BitWrite.OcelotControl.Gateway.Configuration;

/// <summary>
/// Watches for a publication and loads the Snapshot it names.
/// </summary>
/// <remarks>
/// The order is the spec's, not a choice (§19, §20.3). The Gateway consumes a published
/// Snapshot rather than editable management state, and Pub/Sub is a notification
/// rather than a source of truth — the message carries a version and nothing more. So
/// the configuration is retrieved from the immutable snapshot at
/// <c>ocelot:snapshot:{environment}:{version}</c>, and the notification only says
/// which one.
/// <para>
/// The snapshot is read from this gateway's own environment, never from whatever the
/// notification names. A notification that names a different environment is refused
/// rather than followed: the control plane publishing production's snapshot to a
/// development gateway is the cross-environment leak environment isolation exists to
/// prevent, and a gateway that followed the message would serve it.
/// </para>
/// <para>
/// A notification for a snapshot that cannot be read leaves the last configuration in
/// place. A gateway that emptied its file because a read failed would stop routing,
/// which is a worse outcome than being briefly behind.
/// </para>
/// </remarks>
public sealed class ConfigurationSubscriber : BackgroundService
{
    /// <summary>Where the control plane announces a publication (§20.3).</summary>
    public const string PublishedChannel = "ocelot:snapshot:published";

    /// <summary>And a rollback, which is a publication of an earlier snapshot.</summary>
    public const string RolledBackChannel = "ocelot:snapshot:rolled-back";

    private readonly ISubscriber _subscriber;
    private readonly IDatabase _database;
    private readonly OcelotConfigurationWriter _writer;
    private readonly ILogger<ConfigurationSubscriber> _logger;

    public ConfigurationSubscriber(
        IConnectionMultiplexer connectionMultiplexer,
        OcelotConfigurationWriter writer,
        ILogger<ConfigurationSubscriber> logger)
    {
        _subscriber = connectionMultiplexer.GetSubscriber();
        _database = connectionMultiplexer.GetDatabase();
        _writer = writer;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Both channels, because a rollback is how an operator says "go back to that
        // one" and a gateway that ignored it would keep serving the version they
        // just withdrew.
        await _subscriber.SubscribeAsync(PublishedChannel, (_, message) =>
            HandleAsync(message.ToString(), stoppingToken));

        await _subscriber.SubscribeAsync(RolledBackChannel, (_, message) =>
            HandleAsync(message.ToString(), stoppingToken));

        _logger.LogInformation(
            "Subscribed to {Published} and {RolledBack}", PublishedChannel, RolledBackChannel);

        // A gateway starting after a publication never sees the notification, so it
        // asks what is current. §19 puts the Current Published Version directly above
        // the Snapshot in the chain, which is exactly this.
        await LoadCurrentAsync(stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    /// <summary>
    /// Loads the snapshot a notification named, and writes it where Ocelot reads it.
    /// </summary>
    /// <remarks>
    /// The message is a notification, so it is parsed for its version and nothing else
    /// is taken from it. Everything here is guarded: this runs on a background
    /// subscription, and an exception escaping would stop the gateway rather than
    /// leave it serving the configuration it already has.
    /// </remarks>
    public async Task HandleAsync(string message, CancellationToken cancellationToken)
    {
        try
        {
            var publication = ReadPublication(message);

            if (publication is null)
            {
                _logger.LogWarning("Ignoring a notification with no usable version: {Message}", message);
                return;
            }

            if (!PublishedEnvironment.Matches(publication.Environment))
            {
                _logger.LogWarning(
                    "Notification published for environment {Environment} but this gateway serves {Served}; keeping the current configuration",
                    publication.Environment, PublishedEnvironment.KeySegment);
                return;
            }

            await LoadAsync(publication.Version, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not act on the notification; keeping the current configuration");
        }
    }

    /// <summary>
    /// Asks the control plane which version is current, and loads that.
    /// </summary>
    /// <remarks>
    /// Only the published version — never editable state. A gateway that started late
    /// has no notification to act on, and starting with the configuration that was
    /// published is what §19 requires; starting with whatever is being edited would be
    /// serving something no operator approved.
    /// <para>
    /// The current version is stored per environment. If the stored environment does
    /// not match this gateway's environment, the gateway refuses to start (403):
    /// serving a configuration for the wrong environment is the cross-environment leak
    /// this isolation exists to prevent.
    /// </para>
    /// </remarks>
    private async Task LoadCurrentAsync(CancellationToken cancellationToken)
    {
        try
        {
            var current = await _database.StringGetAsync(PublishedEnvironment.RuntimeCurrentKey());

            if (current.IsNullOrEmpty)
            {
                _logger.LogInformation(
                    "No published version recorded for environment {Environment}; starting with the configuration on disk",
                    PublishedEnvironment.KeySegment);
                return;
            }

            var version = ReadVersion(current.ToString());
            var env = ReadEnvironment(current.ToString());

            if (version is null)
                return;

            if (!PublishedEnvironment.Matches(env))
            {
                _logger.LogError(
                    "Current published version is for environment {Env} but this gateway serves {Served}; refusing to start (403)",
                    env, PublishedEnvironment.KeySegment);
                throw new InvalidOperationException($"Environment mismatch: current version is for '{env}', this gateway serves '{PublishedEnvironment.KeySegment}'");
            }

            await LoadAsync(version, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not read the current published version at startup");
            throw;
        }
    }

    /// <summary>
    /// Reads the environment out of <c>ocelot:runtime:current:{env}</c>.
    /// </summary>
    private static string? ReadEnvironment(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return null;

        try
        {
            using var parsed = JsonDocument.Parse(message);
            var root = parsed.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
                return null;

            return ReadProperty(root, "Environment", "environment");
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task LoadAsync(string version, CancellationToken cancellationToken)
    {
        var key = PublishedEnvironment.SnapshotKey(version);
        var stored = await _database.StringGetAsync(key);

        if (stored.IsNullOrEmpty)
        {
            _logger.LogWarning(
                "Notification named version {Version} but {Key} holds nothing; keeping the current configuration",
                version, key);
            return;
        }

        var content = ReadContent(stored.ToString());

        if (content is null)
        {
            _logger.LogError(
                "Snapshot {Version} has no readable content; keeping the current configuration", version);
            return;
        }

        if (await _writer.WriteAsync(content, cancellationToken))
            _logger.LogInformation("Loaded published snapshot {Version}", version);
        else
            _logger.LogWarning(
                "Snapshot {Version} is not a configuration this gateway can serve; keeping the current one",
                version);
    }

    /// <summary>What a publication notification names.</summary>
    private sealed record Publication(string Version, string? Environment);

    /// <summary>
    /// Reads a publication out of a notification.
    /// </summary>
    /// <remarks>
    /// Both casings, because the control plane serialises <c>Version</c> while
    /// <c>ocelot:runtime:current</c> has been seen carrying it as <c>version</c>.
    /// Refusing to guess here would mean ignoring a valid publication.
    /// <para>
    /// The environment is read so it can be <em>refused</em>, not followed. This
    /// gateway loads only its own environment's snapshot; a notification naming
    /// another one is a publication this gateway must not act on.
    /// </para>
    /// </summary>
    private static Publication? ReadPublication(string message)
    {
        if (!TryReadObject(message, out var version, out var environment))
            return null;

        return new Publication(version!, environment);
    }

    /// <summary>
    /// Reads the version out of <c>ocelot:runtime:current</c>.
    /// </summary>
    private static string? ReadVersion(string message) =>
        TryReadObject(message, out var version, out _) ? version : null;

    /// <summary>
    /// Parses a message as a JSON object and pulls two properties out of it.
    /// </summary>
    /// <remarks>
    /// The properties are read inside the parse rather than the element handed back:
    /// a <see cref="JsonElement"/> outlives the document it belongs to only in the
    /// sense that using it afterwards reads freed memory.
    /// </remarks>
    private static bool TryReadObject(string message, out string? version, out string? environment)
    {
        version = null;
        environment = null;

        if (string.IsNullOrWhiteSpace(message))
            return false;

        try
        {
            using var parsed = JsonDocument.Parse(message);
            var root = parsed.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
                return false;

            version = ReadProperty(root, "Version", "version");
            environment = ReadProperty(root, "Environment", "environment");
            return version is not null;
        }
        catch (JsonException)
        {
            return false;
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

    /// <summary>
    /// Reads a snapshot's configuration out of its stored document.
    /// </summary>
    private static string? ReadContent(string document)
    {
        try
        {
            using var parsed = JsonDocument.Parse(document);
            var root = parsed.RootElement;

            return root.TryGetProperty("content", out var content)
                ? content.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}