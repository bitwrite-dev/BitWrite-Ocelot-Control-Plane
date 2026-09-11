using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Domain.ValueObjects.Identity;

namespace BitWrite.OcelotControl.Domain.Services;

/// <summary>
/// Detects duplicate or conflicting routes based on route signature (§12.3).
/// A route signature accounts for HTTP method, upstream path, host, and case sensitivity.
/// </summary>
public class RouteConflictDetector
{
    /// <summary>
    /// Checks if a route conflicts with any existing routes.
    /// </summary>
    /// <param name="newRouteKey">The route key to check for conflicts.</param>
    /// <param name="existingRouteKeys">All existing route keys in the system.</param>
    /// <param name="excludeRouteId">Optional route ID to exclude from conflict check (for updates).</param>
    /// <returns>A list of conflicting route keys.</returns>
    public IReadOnlyList<RouteKey> DetectConflicts(
        RouteKey newRouteKey,
        IEnumerable<(RouteId Id, RouteKey Key)> existingRouteKeys,
        RouteId? excludeRouteId = null)
    {
        var conflicts = new List<RouteKey>();

        foreach (var (id, existingKey) in existingRouteKeys)
        {
            if (excludeRouteId != null && id == excludeRouteId)
                continue;

            if (AreConflicting(newRouteKey, existingKey))
            {
                conflicts.Add(existingKey);
            }
        }

        return conflicts.AsReadOnly();
    }

    /// <summary>
    /// Checks if two routes are conflicting based on their signatures.
    /// </summary>
    public bool AreConflicting(RouteKey route1, RouteKey route2)
    {
        // Same method or one is wildcard
        if (route1.Method.Value != "*" && route2.Method.Value != "*" &&
            !string.Equals(route1.Method.Value, route2.Method.Value, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Check if paths overlap (simplified - exact match for now)
        if (!string.Equals(route1.Path.Value, route2.Path.Value, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Check host compatibility
        if (route1.Host != null && route2.Host != null)
        {
            // Both have host - must match
            if (!string.Equals(route1.Host, route2.Host, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }
        // If one has host and other doesn't, they could conflict depending on Ocelot's matching

        return true;
    }

    /// <summary>
    /// Generates a signature string for a route key.
    /// </summary>
    public string GetRouteSignature(RouteKey routeKey)
    {
        return $"{routeKey.Method.Value}:{routeKey.Path.Value}{(routeKey.Host != null ? $"@{routeKey.Host}" : "")}";
    }
}