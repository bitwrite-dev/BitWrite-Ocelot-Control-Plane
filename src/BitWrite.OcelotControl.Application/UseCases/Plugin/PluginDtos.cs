using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Domain.ValueObjects.Status;

namespace BitWrite.OcelotControl.Application.UseCases.Plugin;

public record PluginResponse(
    PluginId Id,
    string Name,
    string Version,
    string? Description,
    PluginScope Scope,
    bool IsEnabled,
    DateTimeOffset InstalledAt,
    DateTimeOffset? LastUpdated,
    DateTimeOffset? LastEnabled,
    DateTimeOffset? LastDisabled,
    IReadOnlyList<string> Capabilities
);

public record PluginListResponse(
    IReadOnlyList<PluginResponse> Plugins,
    int TotalCount,
    int Page,
    int PageSize
);