namespace BitWrite.OcelotControl.Gateway.Configuration;

/// <summary>
/// The environment this gateway serves, and the two names that follow from it.
/// </summary>
/// <remarks>
/// One setting, not two, because the file and the key have to agree: Ocelot reads its
/// configuration from a file named after the environment, and the control plane stores
/// a published snapshot under the environment it was published for. A gateway that
/// wrote <c>ocelot.Staging.json</c> while Ocelot read <c>ocelot.Production.json</c>, or
/// that loaded a production snapshot into a development file, would serve one
/// environment's routes while claiming another's name — and neither would report it.
///
/// Two spellings rather than one because the two sides disagree: Redis keys carry the
/// normalised lower-case name the control plane's value object produces, and Ocelot's
/// file name is spelled the way Ocelot spells it. Stated here so neither is written
/// out by hand at a call site.
///
/// Derived from constants rather than from <c>ASPNETCORE_ENVIRONMENT</c>: that names
/// how the process is deployed, not which environment's configuration it serves, and
/// the two have to be the same name for the pair to mean anything. Making this
/// configurable is #529, and it has to change both names together.
/// </remarks>
internal static class PublishedEnvironment
{
    /// <summary>The environment as it appears in a Redis key.</summary>
    public const string KeySegment = "production";

    /// <summary>The environment as it appears in the file name Ocelot reads.</summary>
    public const string FileBaseName = "Production";

    /// <summary>The file Ocelot reads and the subscriber writes.</summary>
    public const string ConfigurationFile = $"ocelot.{FileBaseName}.json";

    /// <summary>Where the published snapshot for this environment is stored.</summary>
    public static string SnapshotKey(string version) => $"ocelot:snapshot:{KeySegment}:{version}";

    /// <summary>Where the current published version for this environment is stored.</summary>
    public static string RuntimeCurrentKey() => $"ocelot:runtime:current:{KeySegment}";

    /// <summary>
    /// Whether a notification names this gateway's environment.
    /// </summary>
    /// <remarks>
    /// Compared rather than trusted: the control plane publishing production's snapshot
    /// to a development gateway is the cross-environment leak environment isolation
    /// exists to prevent (#529). Case-insensitive, so either spelling is this
    /// environment and neither is another one.
    ///
    /// A notification that names no environment is accepted. The control plane does not
    /// include one yet (#530), and refusing it would leave every gateway unable to take
    /// a publication at all.
    /// </remarks>
    public static bool Matches(string? named) =>
        string.IsNullOrWhiteSpace(named) ||
        string.Equals(named.Trim(), KeySegment, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(named.Trim(), FileBaseName, StringComparison.OrdinalIgnoreCase);
}
