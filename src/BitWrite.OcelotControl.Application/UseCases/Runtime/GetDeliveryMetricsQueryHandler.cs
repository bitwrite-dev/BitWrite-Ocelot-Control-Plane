using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Application.UseCases.Runtime;

namespace BitWrite.OcelotControl.Application.UseCases.Runtime;

/// <summary>
/// Reports how configuration delivery to gateways has been going.
/// </summary>
/// <remarks>
/// Sourced from what gateways reported when they applied a snapshot. That data was
/// already being written — 172 attempts, unread — so this reads what exists rather
/// than introducing a second thing to keep writing.
/// </remarks>
public class GetDeliveryMetricsQueryHandler
{
    /// <summary>The range used when the caller names none.</summary>
    public static readonly TimeSpan DefaultRange = TimeSpan.FromHours(24);

    /// <summary>
    /// The most attempts one request will read.
    /// </summary>
    /// <remarks>
    /// Attempts live in a single list that is never trimmed, so reading them costs
    /// what has been written. A bound keeps one request's cost flat; what falls
    /// outside it is not counted, which <c>ConsideredAttempts</c> makes visible
    /// rather than leaving it to look like there was nothing to count.
    /// </remarks>
    public const int MaxAttempts = 5000;

    private readonly IDeliveryAttemptRepository _attempts;

    public GetDeliveryMetricsQueryHandler(IDeliveryAttemptRepository attempts)
    {
        _attempts = attempts;
    }

    public async Task<DeliveryMetricsResponse> HandleAsync(
        GetDeliveryMetricsQuery query,
        CancellationToken cancellationToken = default)
    {
        var to = query.To ?? DateTimeOffset.UtcNow;
        var from = query.From ?? to - DefaultRange;
        var limit = Math.Clamp(query.Limit, 1, MaxAttempts);

        var considered = await _attempts.GetRecentAsync(limit, cancellationToken);

        return Aggregate(considered, query with { From = from, To = to, Limit = limit });
    }

    /// <summary>
    /// Aggregates attempts into the report.
    /// </summary>
    /// <remarks>
    /// Separate from the read so the arithmetic can be exercised against known
    /// attempts, which a Redis list cannot provide — it holds whatever has happened,
    /// not what a test means.
    /// </remarks>
    internal static DeliveryMetricsResponse Aggregate(
        IReadOnlyList<DeliveryAttempt> considered,
        GetDeliveryMetricsQuery query)
    {
        var to = query.To ?? DateTimeOffset.UtcNow;
        var from = query.From ?? to - DefaultRange;

        if (to <= from)
            throw new ArgumentException(
                $"The range ends ({to:o}) at or before it starts ({from:o}).", nameof(query));

        var inRange = considered
            .Where(attempt => attempt.AttemptedAt >= from && attempt.AttemptedAt < to)
            .ToList();

        return new DeliveryMetricsResponse(
            from,
            to,
            considered.Count,
            inRange.Count,
            inRange.Count(attempt => attempt.Succeeded),
            inRange.Count(attempt => !attempt.Succeeded),
            Rate(inRange.Count(attempt => attempt.Succeeded), inRange.Count),
            inRange
                .Select(attempt => attempt.SnapshotVersion)
                .Where(version => version.HasValue)
                .Select(version => version!.Value)
                .Distinct()
                .OrderByDescending(version => version)
                .ToList(),
            inRange
                .GroupBy(attempt => attempt.GatewayId)
                .Select(group => Summarise(group.Key, group))
                .OrderByDescending(gateway => gateway.LastAttemptAt)
                .ToList(),
            inRange
                .Where(attempt => !attempt.Succeeded && !string.IsNullOrWhiteSpace(attempt.Error))
                .GroupBy(attempt => attempt.Error)
                .Select(group => new DeliveryErrorFrequency(
                    group.Key,
                    group.Count(),
                    group.Max(attempt => attempt.AttemptedAt)))
                .OrderByDescending(error => error.Occurrences)
                .ThenByDescending(error => error.LastSeenAt)
                .ToList());
    }

    private static GatewayDeliveryMetrics Summarise(Guid gatewayId, IEnumerable<DeliveryAttempt> attempts)
    {
        var all = attempts.ToList();
        var failures = all.Where(attempt => !attempt.Succeeded).ToList();

        return new GatewayDeliveryMetrics(
            gatewayId.ToString(),
            all.Count,
            all.Count(attempt => attempt.Succeeded),
            failures.Count,
            Rate(all.Count(attempt => attempt.Succeeded), all.Count),
            all.Max(attempt => attempt.AttemptedAt),
            failures
                .Select(attempt => attempt.Error)
                .Where(error => !string.IsNullOrWhiteSpace(error))
                .Distinct()
                .ToList());
    }

    /// <summary>
    /// The share that succeeded, or null when nothing was attempted.
    /// </summary>
    /// <remarks>
    /// Null rather than 1 for no attempts, and null rather than 0 for all failures.
    /// Neither is a rate: nothing was asked of the gateways, so there is no
    /// proportion to report, and a number would be read as a healthy one.
    /// </remarks>
    private static double? Rate(int succeeded, int total) =>
        total == 0 ? null : (double)succeeded / total;
}
