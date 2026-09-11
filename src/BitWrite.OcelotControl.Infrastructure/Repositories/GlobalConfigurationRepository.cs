using BitWrite.OcelotControl.Application.Interfaces;
using BitWrite.OcelotControl.Domain.Aggregates.GlobalConfiguration;
using BitWrite.OcelotControl.Infrastructure.Redis;
using StackExchange.Redis;

namespace BitWrite.OcelotControl.Infrastructure.Repositories;

public class RedisGlobalConfigurationRepository : RedisRepositoryBase, IGlobalConfigurationRepository
{
    public RedisGlobalConfigurationRepository(IConnectionMultiplexer connectionMultiplexer) 
        : base(connectionMultiplexer)
    {
    }

    public async Task<GlobalConfiguration> GetAsync(CancellationToken cancellationToken = default)
    {
        var entries = await GetHashAsync(RedisKeyHelper.GlobalConfig);
        
        if (entries.Length == 0)
        {
            // Return default configuration if none exists
            return GlobalConfiguration.Create();
        }

        var config = GlobalConfiguration.Create();
        
        foreach (var entry in entries)
        {
            SetProperty(config, entry.Name, entry.Value.ToString());
        }

        return config;
    }

    public async Task AddAsync(GlobalConfiguration globalConfiguration, CancellationToken cancellationToken = default)
    {
        var entries = new HashEntry[]
        {
            new("Id", globalConfiguration.Id.ToString()),
            new("BaseUrl", globalConfiguration.BaseUrl ?? ""),
            new("RequestIdKey", globalConfiguration.RequestIdKey ?? ""),
            new("DownstreamScheme", globalConfiguration.DownstreamScheme ?? ""),
            new("Timeout", globalConfiguration.Timeout?.ToString() ?? ""),
            new("UpdatedAt", globalConfiguration.UpdatedAt.ToString("O"))
        };

        await SetHashAsync(RedisKeyHelper.GlobalConfig, entries);
    }

    public async Task UpdateAsync(GlobalConfiguration globalConfiguration, CancellationToken cancellationToken = default)
    {
        await AddAsync(globalConfiguration, cancellationToken);
    }

    private void SetProperty(GlobalConfiguration config, string propertyName, string value)
    {
        switch (propertyName)
        {
            case "Id":
                // Id is set during creation
                break;
            case "BaseUrl":
                config.SetBaseUrl(string.IsNullOrEmpty(value) ? null : value);
                break;
            case "RequestIdKey":
                config.SetRequestIdKey(string.IsNullOrEmpty(value) ? null : value);
                break;
            case "DownstreamScheme":
                config.SetDownstreamScheme(string.IsNullOrEmpty(value) ? null : value);
                break;
            case "Timeout":
                if (int.TryParse(value, out var timeout))
                    config.SetTimeout(timeout);
                break;
            case "UpdatedAt":
                // UpdatedAt is managed by the aggregate
                break;
        }
    }
}