using BitWrite.OcelotControl.Domain.ValueObjects.Identity;
using BitWrite.OcelotControl.Domain.ValueObjects.Status;

namespace BitWrite.OcelotControl.Application.UseCases.Plugin;

public record InstallPluginCommand(
    string Id,
    string Name,
    string Version,
    string? Description = null,
    string Scope = "Global",
    string InitiatedBy = "",
    string CorrelationId = ""
);

public record GetPluginQuery(
    PluginId Id
);

public record UninstallPluginCommand(
    PluginId Id,
    string InitiatedBy = "",
    string CorrelationId = ""
);

public record EnablePluginCommand(
    PluginId Id,
    string InitiatedBy = "",
    string CorrelationId = ""
);

public record DisablePluginCommand(
    PluginId Id,
    string InitiatedBy = "",
    string CorrelationId = ""
);

public record UpgradePluginCommand(
    PluginId Id,
    string NewVersion,
    string InitiatedBy = "",
    string CorrelationId = ""
);

public record PluginListQuery(
    int Page = 1,
    int PageSize = 20
);