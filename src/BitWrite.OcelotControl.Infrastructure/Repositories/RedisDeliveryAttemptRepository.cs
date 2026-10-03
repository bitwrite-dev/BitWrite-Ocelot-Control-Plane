using System.Text.Json;
using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Infrastructure.Redis;
using StackExchange.Redis;

namespace BitWrite.OcelotControl.Infrastructure.Repositories;

/// <summary>
/// Reads the delivery attempts gateways have reported.
/// </summary>
/// <remarks>
/// The runtime appends one JSON document per attempt to a list. Nothing read it, so
/// the list grew past 170 entries holding every failure the system had — including
/// two bugs that were being fixed by hand before anyone could see them listed.
/// </remarks>
public class RedisDeliveryAttemptRepository : IDeliveryAttemptRepository
{
    private readonly IDatabase _database;

    public RedisDeliveryAttemptRepository(IConnectionMultiplexer connectionMultiplexer)
    {
        _database = connectionMultiplexer.GetDatabase();
    }

    public async Task<IReadOnlyList<DeliveryAttempt>> GetRecentAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        // The tail, not the whole list: attempts are appended in arrival order, so the
        // most recent are at the end. Reading all of it would make this endpoint
        // slower every week it is left alone.
        var documents = await _database.ListRangeAsync(
            RedisKeyHelper.GatewayActivationResults, -limit, -1);

        var attempts = new List<DeliveryAttempt>(documents.Length);

        foreach (var document in documents)
        {
            var attempt = Parse(document);
            if (attempt is not null)
                attempts.Add(attempt);
        }

        attempts.Reverse();
        return attempts;
    }

    /// <summary>
    /// Reads one reported attempt, or nothing when it cannot be read.
    /// </summary>
    /// <remarks>
    /// A document that does not parse is skipped rather than failing the report. One
    /// unreadable entry — written by an older runtime, truncated, malformed — should
    /// cost its own line, not the metrics for every gateway.
    /// </remarks>
    private static DeliveryAttempt? Parse(RedisValue document)
    {
        if (document.IsNullOrEmpty)
            return null;

        try
        {
            using var parsed = JsonDocument.Parse(document.ToString());
            var root = parsed.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
                return null;

            if (!root.TryGetProperty("GatewayId", out var gatewayId))
                return null;

            if (!Guid.TryParse(gatewayId.GetString(), out var id))
                return null;

            return new DeliveryAttempt(
                id,
                ReadVersion(root),
                !root.TryGetProperty("Success", out var success) || success.GetBoolean(),
                ReadText(root, "Error"),
                ReadTimestamp(root));
        }
        catch (JsonException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            // A field of an unexpected type, e.g. Success as a string.
            return null;
        }
    }

    private static int? ReadVersion(JsonElement root)
    {
        if (!root.TryGetProperty("Version", out var version))
            return null;

        // The runtime writes it as a string; anything readable as a number counts.
        if (version.ValueKind == JsonValueKind.String &&
            int.TryParse(version.GetString(), out var parsed))
            return parsed;

        return version.ValueKind == JsonValueKind.Number && version.TryGetInt32(out var number)
            ? number
            : null;
    }

    private static string ReadText(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    /// <summary>
    /// Reads when the attempt happened, or the epoch when it is not stated.
    /// </summary>
    /// <remarks>
    /// The epoch rather than the current time, which would be a lie: an attempt whose
    /// time is unreadable did not just happen. It falls outside every sane range and
    /// is excluded, rather than counted against whichever window is open.
    /// </remarks>
    private static DateTimeOffset ReadTimestamp(JsonElement root) =>
        DateTimeOffset.TryParse(ReadText(root, "Timestamp"), out var parsed)
            ? parsed
            : DateTimeOffset.UnixEpoch;
}