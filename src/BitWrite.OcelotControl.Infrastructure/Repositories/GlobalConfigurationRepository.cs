using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Domain.Aggregates.GlobalConfiguration;
using BitWrite.OcelotControl.Infrastructure.Redis;
using StackExchange.Redis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BitWrite.OcelotControl.Infrastructure.Repositories;

/// <summary>
/// Global configuration is a singleton, so it lives under one fixed key.
///
/// Stored as a **JSON string**, matching how snapshots are persisted (#430) and
/// the shape already present under `ocelot:global` in Redis. It used to be
/// written as a Redis hash while the existing key held a string, so every read
/// failed with WRONGTYPE — which also made `POST /api/v1/snapshots` return 500,
/// because snapshot creation composes its content from this configuration.
///
/// The nested configuration objects use public setters, so System.Text.Json
/// handles them directly; no mapping is needed for them.
/// </summary>
public class RedisGlobalConfigurationRepository : RedisRepositoryBase, IGlobalConfigurationRepository
{
    /// <summary>
    /// Identifier used when the stored value is not a Guid. The configuration is a
    /// singleton held under one key, so a constant keeps its id stable across
    /// reads. The next write persists it, after which the stored value parses.
    /// </summary>
    private static readonly Guid SingletonId = new("00000000-0000-0000-0000-000000000001");

    public RedisGlobalConfigurationRepository(IConnectionMultiplexer connectionMultiplexer)
        : base(connectionMultiplexer)
    {
    }

    public async Task<GlobalConfiguration> GetAsync(CancellationToken cancellationToken = default)
    {
        var json = await StringGetAsync(RedisKeyHelper.GlobalConfig);

        if (string.IsNullOrWhiteSpace(json))
        {
            // No configuration stored yet: fall back to the domain defaults.
            return GlobalConfiguration.Create();
        }

        var document = RedisSerializer.Deserialize<GlobalConfigurationDocument>(json);
        if (document == null)
        {
            return GlobalConfiguration.Create();
        }

        return GlobalConfiguration.Reconstitute(
            // The id was historically the literal string "default", which is not a
            // Guid. This is a singleton under a single fixed key, so the fallback
            // is a constant rather than a fresh Guid: a random one would change on
            // every read, since a read never writes.
            Guid.TryParse(document.Id, out var id) ? id : SingletonId,
            document.BaseUrl,
            document.RequestIdKey,
            document.DownstreamScheme,
            document.Timeout,
            document.RateLimit?.ToDomain(),
            document.QoS,
            document.HttpHandler,
            document.ServiceDiscovery,
            document.UpdatedAt);
    }

    public async Task AddAsync(GlobalConfiguration globalConfiguration, CancellationToken cancellationToken = default)
    {
        var document = new GlobalConfigurationDocument
        {
            Id = globalConfiguration.Id.ToString(),
            BaseUrl = globalConfiguration.BaseUrl,
            RequestIdKey = globalConfiguration.RequestIdKey,
            DownstreamScheme = globalConfiguration.DownstreamScheme,
            Timeout = globalConfiguration.Timeout,
            RateLimit = RateLimitDocument.FromDomain(globalConfiguration.RateLimit),
            QoS = globalConfiguration.QoS,
            HttpHandler = globalConfiguration.HttpHandler,
            ServiceDiscovery = globalConfiguration.ServiceDiscovery,
            UpdatedAt = globalConfiguration.UpdatedAt,
        };

        await StringSetAsync(RedisKeyHelper.GlobalConfig, RedisSerializer.Serialize(document));
    }

    public async Task UpdateAsync(GlobalConfiguration globalConfiguration, CancellationToken cancellationToken = default)
    {
        await AddAsync(globalConfiguration, cancellationToken);
    }

    /// <summary>
    /// Persisted shape. Mirrors the aggregate but drops <c>DomainEvents</c>,
    /// which are already-recorded facts and would otherwise be replayed on every
    /// read.
    /// </summary>
    private sealed class GlobalConfigurationDocument
    {
        public string? Id { get; set; }
        public string? BaseUrl { get; set; }
        public string? RequestIdKey { get; set; }
        public string? DownstreamScheme { get; set; }
        public int? Timeout { get; set; }
        public RateLimitDocument? RateLimit { get; set; }
        public QoSConfig? QoS { get; set; }
        public HttpHandlerConfig? HttpHandler { get; set; }
        public ServiceDiscoveryConfig? ServiceDiscovery { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }

    /// <summary>
    /// Persisted shape for the rate-limit block.
    ///
    /// `httpStatusCode` is written as a number (429) by the version of this data
    /// that already exists in Redis, but both the domain and the API contract
    /// declare it as a string. The public contract is left alone here — changing
    /// it would ripple into the SDK and the dashboard — so reading accepts either
    /// form and writing emits a string, which normalises the stored value.
    /// </summary>
    private sealed class RateLimitDocument
    {
        public bool EnableRateLimiting { get; set; }

        [JsonConverter(typeof(StringOrNumberConverter))]
        public string? HttpStatusCode { get; set; }

        public static RateLimitDocument? FromDomain(RateLimitConfig? config) =>
            config is null
                ? null
                : new RateLimitDocument
                {
                    EnableRateLimiting = config.EnableRateLimiting,
                    HttpStatusCode = config.HttpStatusCode,
                };

        public RateLimitConfig ToDomain() => new()
        {
            EnableRateLimiting = EnableRateLimiting,
            HttpStatusCode = HttpStatusCode,
        };
    }

    /// <summary>
    /// Reads a string field that may be stored as either a JSON string or a JSON
    /// number, and writes it back as a string.
    /// </summary>
    private sealed class StringOrNumberConverter : JsonConverter<string?>
    {
        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.TokenType switch
            {
                JsonTokenType.String => reader.GetString(),
                JsonTokenType.Number => reader.TryGetInt64(out var number)
                    ? number.ToString()
                    : reader.GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture),
                JsonTokenType.Null => null,
                _ => throw new JsonException($"Unexpected token {reader.TokenType} for a string value"),
            };

        public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNullValue();
                return;
            }
            writer.WriteStringValue(value);
        }
    }
}
