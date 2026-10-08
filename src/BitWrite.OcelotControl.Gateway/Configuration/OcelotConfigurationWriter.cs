using System.Text.Json;
using System.Text.Json.Nodes;


namespace BitWrite.OcelotControl.Gateway.Configuration;

/// <summary>
/// Writes the configuration Ocelot reads, and nothing else.
/// </summary>
/// <remarks>
/// Ocelot 18 loads its configuration from a file and re-reads it on a poll. This is
/// the whole seam between the control plane and Ocelot: no internal interface is
/// replaced, no registration order depends on Ocelot's internals, and the file stays
/// readable for an operator who wants to see what a gateway is actually running.
/// </remarks>
public sealed class OcelotConfigurationWriter
{
    /// <summary>
    /// Indented, so the file an operator opens to see what a gateway is running is
    /// readable rather than one long line.
    /// </summary>
    private static readonly JsonSerializerOptions Readable = new() { WriteIndented = true };

    private readonly string _directory;
    private readonly string _fileName;
    private readonly ConfigurationReloader? _reloader;

    /// <param name="directory">Where the configuration file lives.</param>
    /// <param name="environmentName">
    /// Ocelot reads <c>ocelot.{Environment}.json</c> — <c>ocelot.Production.json</c> by
    /// default — so the file written has to be the one it reads. Writing a plain
    /// <c>ocelot.json</c> would put the published configuration somewhere nothing looks,
    /// and the gateway would serve whatever it started with and report no reason.
    /// </param>
    public OcelotConfigurationWriter(
        string directory,
        string environmentName = "Production",
        ConfigurationReloader? reloader = null)
    {
        _directory = directory;
        _fileName = $"ocelot.{environmentName}.json";
        _reloader = reloader;
    }

    public string ConfigPath => Path.Combine(_directory, _fileName);

    /// <summary>
    /// Writes a snapshot's configuration, and reports whether it was written.
    /// </summary>
    /// <remarks>
    /// Written in place rather than renamed over, because Ocelot watches the path it
    /// was given: a rename replaces the file behind it, and a watcher holding the old
    /// handle never sees the new content. A truncated read is the trade, and the safer
    /// one here — the worst case is a poll that reads a partial file and keeps the
    /// configuration it already has.
    /// </remarks>
    public async Task<bool> WriteAsync(
        string snapshotContent,
        CancellationToken cancellationToken = default)
    {
        var ocelotJson = ToOcelotConfiguration(snapshotContent);

        if (ocelotJson is null)
            return false;

        Directory.CreateDirectory(_directory);

        await File.WriteAllTextAsync(ConfigPath, ocelotJson, cancellationToken);

        // Writing the file is only half of it: Ocelot reloads when its change token is
        // activated, and without this it would go on serving whatever it read at
        // startup with the published configuration sitting unread on disk.
        _reloader?.Activate();

        return true;
    }

    /// <summary>
    /// Converts a stored snapshot into the shape Ocelot reads.
    /// </summary>
    /// <remarks>
    /// A snapshot holds the canonical form the control plane hashes and compares —
    /// lower-case <c>global</c> and <c>routes</c> — while Ocelot expects
    /// <c>GlobalConfiguration</c> and <c>Routes</c>. Written straight through, Ocelot
    /// binds nothing and answers 404 for every path, which looks exactly like a
    /// gateway with no routes rather than one with a mis-shaped file.
    /// <para>
    /// Routes are copied through as they are: their fields already match Ocelot's,
    /// because the canonical form is built from Ocelot's own route model.
    /// </para>
    /// </remarks>
    internal static string? ToOcelotConfiguration(string snapshotContent)
    {
        if (string.IsNullOrWhiteSpace(snapshotContent))
            return null;

        JsonNode? parsed;

        try
        {
            parsed = JsonNode.Parse(snapshotContent);
        }
        catch (JsonException)
        {
            return null;
        }

        if (parsed is not JsonObject canonical)
            return null;

        // A snapshot already in Ocelot's shape is passed through untouched, so a
        // control plane that starts emitting that shape needs no change here.
        if (canonical.ContainsKey("Routes") && canonical.ContainsKey("GlobalConfiguration"))
            return canonical.ToJsonString(Readable);

        var routes = ConvertRoutes(canonical["routes"] as JsonArray);

        var global = canonical["global"] as JsonObject ?? new JsonObject();

        var ocelot = new JsonObject
        {
            ["Routes"] = routes,
            ["GlobalConfiguration"] = new JsonObject
            {
                ["BaseUrl"] = global["baseUrl"]?.GetValue<string>() ?? "http://localhost:5000",
                ["RequestIdKey"] = global["requestIdKey"]?.GetValue<string>() ?? "X-Request-Id",
            },
        };

        return ocelot.ToJsonString(Readable);
    }

    /// <summary>
    /// Rewrites each route's fields into the casing Ocelot binds.
    /// </summary>
    /// <remarks>
    /// The canonical form uses lower-case field names, and Ocelot's JSON binding is
    /// case-sensitive: it looks for <c>UpstreamPathTemplate</c> and does not find
    /// <c>upstreamPathTemplate</c>. Copied through as-is, every route binds to
    /// defaults — a null template and no downstream — so Ocelot matches nothing and
    /// answers 404 for every path while the file plainly lists routes. That reads as
    /// a gateway with no configuration rather than a mis-cased one.
    /// <para>
    /// Known fields are mapped by name; anything else is carried over with its casing
    /// intact, so a field this version does not know about still reaches the file
    /// rather than being dropped.
    /// </para>
    /// </remarks>
    private static JsonArray ConvertRoutes(JsonArray? canonicalRoutes)
    {
        var converted = new JsonArray();

        if (canonicalRoutes is null)
            return converted;

        foreach (var route in canonicalRoutes.OfType<JsonObject>())
        {
            var ocelotRoute = new JsonObject();

            foreach (var (name, value) in route)
            {
                if (value is null)
                    continue;

                ocelotRoute[OcelotFieldName(name)] = ConvertValue(name, value);
            }

            converted.Add(ocelotRoute);
        }

        return converted;
    }

    /// <summary>
    /// Rewrites a value whose own fields are cased, not just the field itself.
    /// </summary>
    /// <remarks>
    /// A downstream host list is objects with their own fields, so renaming only the
    /// outer name leaves <c>host</c> and <c>port</c> inside it — and Ocelot binds
    /// <c>Host</c> and <c>Port</c>, so every route would point at nothing. Same
    /// reason as the field names above, one level down.
    /// </remarks>
    private static JsonNode? ConvertValue(string fieldName, JsonNode? value) =>
        fieldName == "downstreamHostAndPorts" && value is JsonArray hosts
            ? ConvertHosts(hosts)
            : value.DeepClone();

    private static JsonArray ConvertHosts(JsonArray hosts)
    {
        var converted = new JsonArray();

        foreach (var host in hosts.OfType<JsonObject>())
        {
            var ocelotHost = new JsonObject();

            foreach (var (name, value) in host)
            {
                if (value is null)
                    continue;

                ocelotHost[name switch
                {
                    "host" => "Host",
                    "port" => "Port",
                    _ => name,
                }] = value.DeepClone();
            }

            converted.Add(ocelotHost);
        }

        return converted;
    }

    /// <summary>
    /// The name Ocelot knows a canonical field by.
    /// </summary>
    private static string OcelotFieldName(string canonicalName) =>
        canonicalName switch
        {
            "upstreamPathTemplate" => "UpstreamPathTemplate",
            "upstreamHttpMethod" => "UpstreamHttpMethod",
            "upstreamQueryString" => "UpstreamQueryString",
            "upstreamHeaders" => "UpstreamHeaders",
            "downstreamPathTemplate" => "DownstreamPathTemplate",
            "downstreamScheme" => "DownstreamScheme",
            "downstreamHostAndPorts" => "DownstreamHostAndPorts",
            "downstreamQueryString" => "DownstreamQueryString",
            "downstreamHeaders" => "DownstreamHeaders",
            "authenticationOptions" => "AuthenticationOptions",
            "authorizationOptions" => "AuthorizationOptions",
            "rateLimitOptions" => "RateLimitOptions",
            "qosOptions" => "QoSOptions",
            "fileCacheOptions" => "FileCacheOptions",
            "loadBalancerOptions" => "LoadBalancerOptions",
            "scopes" => "Scopes",
            "key" => "Key",
            "priority" => "Priority",
            "routeIsCaseSensitive" => "RouteIsCaseSensitive",
            "dangerousAcceptAnyServerCertificateValidator" =>
                "DangerousAcceptAnyServerCertificateValidator",
            _ => canonicalName,
        };
}
