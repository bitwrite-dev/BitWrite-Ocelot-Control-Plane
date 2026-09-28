using BitWrite.OcelotControl.Domain.Aggregates.SystemSettings;

namespace BitWrite.OcelotControl.Application.Interfaces;

/// <summary>
/// Reads and writes the one and only system settings row.
/// </summary>
/// <remarks>
/// A singleton, so there is no id in any method. <see cref="GetAsync"/> returns
/// the settings whether or not they have ever been written, which is what lets a
/// first-run screen ask "has a version been chosen yet" without a separate
/// existence check.
/// </remarks>
public interface ISystemSettingsRepository
{
    /// <summary>
    /// The current settings, or a fresh uninitialised instance if none exist yet.
    /// </summary>
    Task<SystemSettings> GetAsync(CancellationToken cancellationToken = default);

    Task UpdateAsync(SystemSettings settings, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes the settings only if none exist yet.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="UpdateAsync"/> because first-run and a later
    /// write must not be the same operation: the version is chosen exactly once,
    /// and a create-if-absent makes two concurrent first-run attempts lose one
    /// of the choices rather than both applying.
    /// </remarks>
    /// <returns>True if this call created the settings, false if they already existed.</returns>
    Task<bool> TryCreateAsync(SystemSettings settings, CancellationToken cancellationToken = default);
}
