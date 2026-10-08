using System.Text.Json;
using StackExchange.Redis;

namespace BitWrite.OcelotControl.Application.UseCases.Runtime;

/// <summary>
/// Reads what a gateway last reported about itself, from the key the runtime writes.
/// </summary>
/// <remarks>
/// A gateway record is a JSON string. The runtime writes one on every heartbeat and
/// <c>RuntimeInstanceRepository</c> writes and reads the same shape, so that is the
/// shape on the key. Asking Redis for a hash there is not an empty answer — it is
/// WRONGTYPE, which took down the whole gateway endpoint and with it the overview
/// page.
/// <para>
/// Shared by the three handlers that need it, because they each grew their own copy
/// of the same wrong read.
/// </para>
/// </remarks>
internal static class GatewayRuntimeInfoReader
{
    /// <summary>
    /// Reads the reported runtime info, or nothing when the gateway has not reported.
    /// </summary>
    /// <remarks>
    /// A key that is absent, empty or unreadable yields an empty dictionary. Runtime
    /// info is a report, not the gateway's identity: a gateway missing its report is
    /// still a gateway, and the identity and status come from the instance record.
    /// Failing here would cost the caller a gateway that is demonstrably registered.
    /// </remarks>
    public static async Task<IReadOnlyDictionary<string, string>> ReadAsync(
        IDatabase database,
        Guid gatewayId)
    {
        var document = await database.StringGetAsync($"ocelot:runtime:gateway:{gatewayId}");

        if (document.IsNullOrEmpty)
            return new Dictionary<string, string>();

        try
        {
            using var parsed = JsonDocument.Parse(document.ToString());
            if (parsed.RootElement.ValueKind != JsonValueKind.Object)
                return new Dictionary<string, string>();

            return parsed.RootElement
                .EnumerateObject()
                .ToDictionary(
                    property => property.Name,
                    property => property.Value.ValueKind switch
                    {
                        JsonValueKind.String => property.Value.GetString() ?? string.Empty,
                        JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
                        _ => property.Value.GetRawText(),
                    },
                    StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            // Malformed or unexpected shape: report no runtime info rather than
            // failing the gateway. The instance record is what the list is built from.
            return new Dictionary<string, string>();
        }
    }
}
