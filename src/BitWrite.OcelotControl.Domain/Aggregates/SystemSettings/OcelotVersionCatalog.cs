using BitWrite.OcelotControl.Domain.Exceptions;
using BitWrite.OcelotControl.Domain.Services;

namespace BitWrite.OcelotControl.Domain.Aggregates.SystemSettings;

using OcelotVersion = BitWrite.OcelotControl.Domain.ValueObjects.Configuration.OcelotVersion;

/// <summary>
/// The Ocelot versions this control plane can actually generate configuration for.
/// </summary>
/// <remarks>
/// The list is derived from what the emitter can express, not from what Ocelot
/// has published. That distinction is the whole point: offering a version whose
/// shapes are unknown would mean generating a file the target cannot read, and
/// nothing would surface until a gateway failed to start.
/// <para>
/// Adding a version here is therefore a statement that its shapes have been
/// established and emitted. A flag flip would be the wrong kind of change. When
/// 19 and 20 are worked out they join this list, and the refusals in the emitter
/// are re-examined per version at the same time.
/// </para>
/// </remarks>
public static class OcelotVersionCatalog
{
    /// <summary>
    /// Versions whose configuration shapes are known and emitted.
    /// </summary>
    private static readonly IReadOnlyList<OcelotVersion> Supported = new[]
    {
        OcelotVersion.V18_0
    };

    /// <summary>
    /// Versions a deployment may choose from, newest first.
    /// </summary>
    public static IReadOnlyList<OcelotVersion> EmittableVersions() =>
        Supported.OrderByDescending(version => version).ToList();

    public static bool IsEmittable(OcelotVersion version) =>
        Supported.Any(candidate => candidate.CompareTo(version) == 0);

    /// <summary>
    /// The version to emit against, refusing rather than assuming one.
    /// </summary>
    /// <remarks>
    /// A control plane that has not been initialised has no version, and
    /// generating configuration anyway would publish a file shaped for a version
    /// nobody chose. The caller is expected to have surfaced the first-run
    /// choice first.
    /// </remarks>
    public static OcelotVersion RequireConfigured(OcelotVersion? configured, string correlationId = "")
    {
        if (configured is null)
        {
            throw new DomainException(
                "No Ocelot version has been chosen yet. The version is selected once, during " +
                "first-run setup, and every generated configuration depends on it.",
                "OCELOT_VERSION_NOT_CONFIGURED");
        }

        if (!IsEmittable(configured))
        {
            throw new NotExpressibleException(
                "ocelotVersion",
                $"Ocelot {configured} is recorded as the configured version but its shapes are " +
                "not established, so no configuration can be generated for it");
        }

        return configured;
    }
}
