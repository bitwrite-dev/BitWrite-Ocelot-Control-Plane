using BitWrite.OcelotControl.Domain.Exceptions;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.FeatureConfig;

namespace BitWrite.OcelotControl.Domain.Services;

/// <summary>
/// A value the target Ocelot version cannot express in its configuration.
/// </summary>
/// <remarks>
/// Thrown rather than dropped. A silently omitted rule is the worst outcome
/// available: the operator configures authorization, the build succeeds, and the
/// gateway ignores it.
/// </remarks>
public class NotExpressibleException : DomainException
{
    /// <summary>The request field the value came from, for reporting.</summary>
    public string Field { get; }

    public NotExpressibleException(string field, string reason)
        : base($"{field} cannot be expressed in the generated configuration: {reason}", "NOT_EXPRESSIBLE")
    {
        Field = field;
    }
}

/// <summary>
/// Maps the domain's feature options onto an Ocelot configuration.
/// </summary>
/// <remarks>
/// Written against Ocelot 18, the minimum supported version. Emitting the oldest
/// shape is what lets a single snapshot reach 18, 19 and 20 alike, and 18 is
/// where the reference for these field names comes from.
/// <para>
/// Later versions reworked the transformation blocks — 20 replaced the paired
/// header dictionaries and added \`Remove\` — so this is the one place a
/// version-specific difference would go, and it is deliberately keyed off the
/// version argument the builder already accepts and previously ignored.
/// </para>
/// <para>
/// The domain models the 20 shape: \`Add\`/\`Remove\`/\`Transform\`. In 18 there is
/// no \`Remove\` and no separate transform block, so those values are rejected
/// rather than quietly dropped. Mapping them onto a field that means something
/// else would be worse than saying no.
/// </para>
/// </remarks>
internal static class FeatureOptionEmitter
{
    public static OcelotClaimsRequirement? Authorization(
        AuthorizationOptions? options,
        OcelotVersion version)
    {
        if (options == null) return null;

        // 18 has one mechanism: a flat claim requirement. Policies and scopes
        // have no counterpart, and inventing one would be a silent no-op at the
        // gateway.
        if (options.Policies.Count > 0)
        {
            throw new NotExpressibleException(
                "authorizationOptions.policies",
                "Ocelot 18 has no route-level policy; only claim requirements are supported");
        }

        if (options.Scopes.Count > 0)
        {
            throw new NotExpressibleException(
                "authorizationOptions.scopes",
                "Ocelot 18 has no route-level scope; scopes belong to AuthenticationOptions.AllowedScopes");
        }

        if (options.Requirements.Count == 0) return null;

        return new OcelotClaimsRequirement { Claims = new Dictionary<string, string>(options.Requirements) };
    }

    public static Dictionary<string, string>? AddClaimsToRequest(
        ClaimOptions? options,
        OcelotVersion version)
    {
        if (options == null) return null;

        // 18 models claims transformation as four named blocks, each a dictionary
        // whose value carries its own expression. There is no remove and no
        // separate transform list.
        RejectUnsupported(options.Remove, "claimTransformations.remove",
            "Ocelot 18 has no way to remove a claim from a request");
        RejectUnsupported(options.Transform, "claimTransformations.transform",
            $"Ocelot {version} expresses a rewrite as the value of an AddClaimsToRequest " +
            "entry rather than a separate block");

        return ToDictionary(options.Add);
    }

    public static Dictionary<string, string>? UpstreamHeaderTransform(
        HeaderOptions? options,
        OcelotVersion version)
    {
        if (options == null) return null;

        RejectUnsupported(options.Remove, "headerTransformations.remove",
            $"Ocelot {version} has no way to remove a header");

        return ToDictionary(options.Transform);
    }

    public static Dictionary<string, string>? DownstreamHeaderTransform(
        HeaderOptions? options,
        OcelotVersion version)
    {
        if (options == null) return null;

        return ToDictionary(options.Add);
    }

    public static void RejectQueryTransformations(QueryOptions? options, OcelotVersion version)
    {
        if (options == null) return;

        var total = options.Add.Count + options.Remove.Count + options.Transform.Count;
        if (total == 0) return;

        // A route's query string is shaped by its path template in 18. The only
        // query block is AddQueriesToRequest, which is sourced from claims, and
        // that is emitted as part of the claims transformation above.
        throw new NotExpressibleException(
            "queryTransformations",
            $"Ocelot {version} shapes the query string through the path template; only " +
            "claim-sourced AddQueriesToRequest is available, which is not what this block describes");
    }

    private static Dictionary<string, string>? ToDictionary<T>(List<T>? entries)
        where T : class
    {
        if (entries == null || entries.Count == 0) return null;

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            var key = Read(entry, "Key");
            if (string.IsNullOrWhiteSpace(key))
                throw new NotExpressibleException("transformations", "An entry has no key");

            if (!result.TryAdd(key, Read(entry, "Value") ?? string.Empty))
            {
                throw new NotExpressibleException(
                    "transformations",
                    $"'{key}' is listed more than once");
            }
        }

        return result;
    }

    private static void RejectUnsupported<T>(List<T>? entries, string field, string reason)
    {
        if (entries != null && entries.Count > 0)
        {
            throw new NotExpressibleException(field, reason);
        }
    }

    /// <summary>
    /// Reads the shared key and value the three transform flavours all carry.
    /// </summary>
    private static string? Read(object entry, string property) =>
        entry.GetType().GetProperty(property)?.GetValue(entry) as string;
}
