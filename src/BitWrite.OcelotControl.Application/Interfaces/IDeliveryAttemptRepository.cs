namespace BitWrite.OcelotControl.Application.Interfaces;

/// <summary>
/// One attempt by a gateway to apply a configuration.
/// </summary>
/// <param name="GatewayId">Which gateway tried.</param>
/// <param name="SnapshotVersion">Which version it was trying to apply.</param>
/// <param name="Succeeded">Whether it applied.</param>
/// <param name="Error">Why not, verbatim, when it did not.</param>
/// <param name="AttemptedAt">When the gateway reported the attempt.</param>
/// <remarks>
/// Reported by the gateway rather than decided here, because only the gateway knows
/// whether Ocelot accepted the configuration. The control plane's own publication
/// record says what was asked for; this says what happened.
/// </remarks>
public record DeliveryAttempt(
    Guid GatewayId,
    int? SnapshotVersion,
    bool Succeeded,
    string Error,
    DateTimeOffset AttemptedAt);

/// <summary>
/// Where gateways' delivery attempts are recorded.
/// </summary>
public interface IDeliveryAttemptRepository
{
    /// <summary>
    /// The most recent attempts, newest first.
    /// </summary>
    /// <param name="limit">
    /// How many to read at most. The attempts are a single append-only list, so
    /// reading them costs what has been written; this bounds one request's work.
    /// </param>
    Task<IReadOnlyList<DeliveryAttempt>> GetRecentAsync(int limit, CancellationToken cancellationToken = default);
}