using Ocelot.Configuration.ChangeTracking;

namespace BitWrite.OcelotControl.Gateway.Configuration;

/// <summary>
/// Tells Ocelot the file it is watching has changed.
/// </summary>
/// <remarks>
/// Ocelot 18 decides whether to reload by watching the configuration file, through
/// <see cref="IOcelotConfigurationChangeTokenSource"/>. Writing the file is not enough
/// on its own: the token has to be activated, or Ocelot keeps serving the configuration
/// it read at startup. The poller alone does not do it — it asks the repository, and
/// the repository asks the token.
/// <para>
/// Activating immediately after a write is also what makes a publication land at once,
/// rather than whenever the next poll happens to run.
/// </para>
/// </remarks>
public sealed class ConfigurationReloader
{
    private readonly IOcelotConfigurationChangeTokenSource? _changeTokenSource;
    private readonly ILogger<ConfigurationReloader> _logger;

    public ConfigurationReloader(
        IOcelotConfigurationChangeTokenSource? changeTokenSource,
        ILogger<ConfigurationReloader> logger)
    {
        _changeTokenSource = changeTokenSource;
        _logger = logger;
    }

    /// <summary>
    /// Signals that the configuration on disk is now different.
    /// </summary>
    /// <remarks>
    /// Best-effort by design: the subscription runs on a background task, and failing
    /// here would have to be handled rather than thrown. Ocelot still reloads on its own
    /// poll if this does not take, which is slower rather than broken — so a failure is
    /// logged and swallowed rather than taken as a reason to stop serving.
    /// </remarks>
    public void Activate()
    {
        if (_changeTokenSource is null)
        {
            _logger.LogDebug(
                "No configuration change token source is registered; Ocelot will reload on its next poll");
            return;
        }

        try
        {
            _changeTokenSource.Activate();
            _logger.LogInformation("Signalled that the configuration file changed");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not signal the configuration change; Ocelot will reload on its next poll");
        }
    }
}
