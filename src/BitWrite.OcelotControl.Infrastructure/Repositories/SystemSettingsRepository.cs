using System.Text.Json;
using System.Text.Json.Serialization;
using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Domain.Aggregates.SystemSettings;
using BitWrite.OcelotControl.Domain.ValueObjects.Configuration;
using BitWrite.OcelotControl.Infrastructure.Redis;
using StackExchange.Redis;
using OcelotVersion = BitWrite.OcelotControl.Domain.ValueObjects.Configuration.OcelotVersion;

namespace BitWrite.OcelotControl.Infrastructure.Repositories;

/// <summary>
/// System settings is a singleton, so it lives under one fixed key, stored as a
/// JSON string like the global configuration.
/// </summary>
public class RedisSystemSettingsRepository : RedisRepositoryBase, ISystemSettingsRepository
{
    public RedisSystemSettingsRepository(IConnectionMultiplexer connectionMultiplexer)
        : base(connectionMultiplexer)
    {
    }

    public async Task<SystemSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        var json = await StringGetAsync(RedisKeyHelper.SystemSettings);

        if (string.IsNullOrWhiteSpace(json))
        {
            // Never written: this is the state the first-run screen exists for.
            return SystemSettings.Create();
        }

        var document = RedisSerializer.Deserialize<SystemSettingsDocument>(json);
        if (document is null)
        {
            return SystemSettings.Create();
        }

        return SystemSettings.Reconstitute(
            // Stored as a string because OcelotVersion has a private constructor
            // and no parameterless one, so System.Text.Json cannot bind it.
            ParseVersion(document.OcelotVersion),
            document.OcelotVersionSelectedAt,
            document.OcelotVersionSelectedBy,
            document.PollIntervalSeconds,
            document.AuditLogRetentionDays,
            document.SnapshotRetentionCount,
            document.CreatedAt,
            document.UpdatedAt);
    }

    public async Task UpdateAsync(SystemSettings settings, CancellationToken cancellationToken = default)
    {
        await StringSetAsync(RedisKeyHelper.SystemSettings, Serialize(settings));
    }

    public async Task<bool> TryCreateAsync(SystemSettings settings, CancellationToken cancellationToken = default)
    {
        return await StringSetIfNotExistsAsync(RedisKeyHelper.SystemSettings, Serialize(settings));
    }

    private static OcelotVersion? ParseVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        try
        {
            return OcelotVersion.Parse(value);
        }
        catch (Exception)
        {
            // A value this build cannot read is treated as "not chosen", which
            // puts the installation back into first-run rather than failing every
            // read of the settings. Choosing again is refused if the stored value
            // is still there, so this is a degraded state, not a silent reset.
            return null;
        }
    }

    /// <remarks>
    /// Written with the same camelCase naming <c>RedisSerializer</c> reads back
    /// with. Writing PascalCase here while the read expects camelCase loses every
    /// field silently, which is exactly what a first test run found.
    /// </remarks>
    private static string Serialize(SystemSettings settings) =>
        JsonSerializer.Serialize(ToDocument(settings), new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });

    private static SystemSettingsDocument ToDocument(SystemSettings settings) => new(
        settings.Id.ToString(),
        settings.OcelotVersion?.ToString(),
        settings.OcelotVersionSelectedAt,
        settings.OcelotVersionSelectedBy,
        settings.PollIntervalSeconds,
        settings.AuditLogRetentionDays,
        settings.SnapshotRetentionCount,
        settings.CreatedAt,
        settings.UpdatedAt);

    private sealed record SystemSettingsDocument(
        string Id,
        string? OcelotVersion,
        DateTimeOffset? OcelotVersionSelectedAt,
        string? OcelotVersionSelectedBy,
        int PollIntervalSeconds,
        int AuditLogRetentionDays,
        int SnapshotRetentionCount,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);
}
