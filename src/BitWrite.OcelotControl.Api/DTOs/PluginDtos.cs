using System.ComponentModel.DataAnnotations;

namespace BitWrite.OcelotControl.Api.DTOs;

public record InstallPluginRequest(
    [Required][MaxLength(100)] string Id,
    [Required][MaxLength(200)] string Name,
    [Required][MaxLength(50)] string Version,
    [MaxLength(1000)] string? Description,
    string Scope = "Global"
);

public record UpdatePluginRequest(
    [MaxLength(200)] string? Name,
    [MaxLength(50)] string? Version,
    [MaxLength(1000)] string? Description
);

public record PluginResponse(
    string Id,
    string Name,
    string Version,
    string? Description,
    string Scope,
    bool IsEnabled,
    DateTimeOffset InstalledAt,
    DateTimeOffset? LastUpdated,
    DateTimeOffset? LastEnabled,
    DateTimeOffset? LastDisabled
);

public record PluginListResponse(
    List<PluginResponse> Plugins,
    int TotalCount,
    int Page,
    int PageSize
);