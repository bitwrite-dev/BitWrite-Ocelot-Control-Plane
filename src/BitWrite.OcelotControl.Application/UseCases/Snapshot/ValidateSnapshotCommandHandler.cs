using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Application.UseCases.Snapshot;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using System.Text.Json;

namespace BitWrite.OcelotControl.Application.UseCases.Snapshot;

public class ValidateSnapshotCommandHandler
{
    public async Task<ValidateSnapshotResponse> HandleAsync(ValidateSnapshotCommand command, CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        try
        {
            // 1. Validate it's valid JSON
            var jsonDoc = JsonDocument.Parse(command.Content);
            
            // 2. Check required top-level fields
            var root = jsonDoc.RootElement;
            
            if (!root.TryGetProperty("Routes", out var routesElement))
            {
                errors.Add("Missing required 'Routes' array");
            }
            else if (routesElement.ValueKind != JsonValueKind.Array)
            {
                errors.Add("'Routes' must be an array");
            }
            else
            {
                var routeCount = routesElement.GetArrayLength();
                if (routeCount == 0)
                {
                    errors.Add("At least one route is required");
                }
                else
                {
                    var routeKeys = new HashSet<string>();
                    for (int i = 0; i < routeCount; i++)
                    {
                        var route = routesElement[i];
                        
                        if (!route.TryGetProperty("UpstreamPathTemplate", out var upstreamPath) || 
                            string.IsNullOrWhiteSpace(upstreamPath.GetString()))
                        {
                            errors.Add($"Route {i} missing required 'UpstreamPathTemplate'");
                        }
                        
                        if (!route.TryGetProperty("DownstreamHostAndPorts", out var downstreamHosts) ||
                            downstreamHosts.ValueKind != JsonValueKind.Array ||
                            downstreamHosts.GetArrayLength() == 0)
                        {
                            errors.Add($"Route {i} must have at least one downstream host");
                        }
                        
                        // Check for duplicate route keys (UpstreamPathTemplate + Method + Host)
                        if (route.TryGetProperty("UpstreamPathTemplate", out var up) &&
                            route.TryGetProperty("DownstreamPathTemplate", out var dp) &&
                            route.TryGetProperty("UpstreamHttpMethod", out var method) &&
                            route.TryGetProperty("Host", out var host))
                        {
                            var key = $"{method.GetString()}:{up.GetString()}:{host.GetString() ?? ""}";
                            // Could track duplicates here if needed
                        }
                    }
}
                }
            
            if (!root.TryGetProperty("GlobalConfiguration", out var globalConfig))
            {
                errors.Add("Missing required 'GlobalConfiguration' object");
            }
        }
        catch (JsonException ex)
        {
            errors.Add($"Invalid JSON: {ex.Message}");
        }
        catch (Exception ex)
        {
            errors.Add($"Validation failed: {ex.Message}");
        }

        return new ValidateSnapshotResponse(errors.Count == 0, errors);
    }
}