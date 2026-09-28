using System.Text.Json;

namespace BitWrite.OcelotControl.Domain.Services;

/// <summary>
/// Reads the composition of a generated Ocelot configuration.
/// </summary>
/// <remarks>
/// A snapshot's content is the Ocelot document, and the spec asks a snapshot list
/// to show how many routes and services it carries and which plugin versions it
/// pins. Those numbers were not recorded anywhere, so they are read back out of
/// the document itself rather than stored separately — a second copy could
/// disagree with the file it describes, and the file is what a gateway runs.
/// <para>
/// A document that cannot be parsed yields nulls rather than throwing. The counts
/// are reporting, not correctness: a snapshot whose content cannot be read is
/// still a snapshot, and failing to load the list because one of them is
/// malformed would be a worse outcome than showing a dash.
/// </para>
/// </remarks>
public static class SnapshotComposition
{
    private static readonly JsonDocumentOptions Options = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip
    };

    /// <summary>How many routes the document declares, or null if it cannot be read.</summary>
    public static int? RouteCount(string? content) => CountArray(content, "Routes");

    /// <summary>How many services the document declares, or null if it cannot be read.</summary>
    public static int? ServiceCount(string? content) => CountArray(content, "Services");

    /// <summary>
    /// The plugin versions the document pins.
    /// </summary>
    /// <remarks>
    /// The generated configuration carries plugin configuration alongside the
    /// routes. When a deployment has no plugins there is nothing to report, which
    /// is an empty list rather than an unknown.
    /// </remarks>
    public static IReadOnlyList<string> PluginVersions(string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return Array.Empty<string>();

        try
        {
            using var document = JsonDocument.Parse(content, Options);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return Array.Empty<string>();

            // The canonical writer names the section, so this reads the same shape
            // the file a gateway would see.
            var section = FindProperty(document.RootElement, "PluginConfigurations")
                          ?? FindProperty(document.RootElement, "Plugins");

            if (section is not { ValueKind: JsonValueKind.Array }) return Array.Empty<string>();

            var versions = new List<string>();
            foreach (var entry in section.Value.EnumerateArray())
            {
                var version = FindProperty(entry, "Version")?.GetString();
                var name = FindProperty(entry, "Name")?.GetString();
                if (string.IsNullOrWhiteSpace(version) && string.IsNullOrWhiteSpace(name))
                    continue;

                versions.Add(string.IsNullOrWhiteSpace(name) ? version! : $"{name} {version}");
            }

            return versions;
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }

    private static int? CountArray(string? content, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(content)) return null;

        try
        {
            using var document = JsonDocument.Parse(content, Options);
            if (document.RootElement.ValueKind != JsonValueKind.Object) return null;

            var array = FindProperty(document.RootElement, propertyName);
            if (array is not { ValueKind: JsonValueKind.Array }) return null;

            return array.Value.GetArrayLength();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Finds a property regardless of casing.
    /// </summary>
    /// <remarks>
    /// The document is written in more than one casing across the code paths that
    /// produce it, and a report that silently shows nothing because of a capital
    /// letter is worse than the casing being looked up.
    /// </remarks>
    private static JsonElement? FindProperty(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;

        if (element.TryGetProperty(name, out var exact)) return exact;

        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                return property.Value;
        }

        return null;
    }
}
