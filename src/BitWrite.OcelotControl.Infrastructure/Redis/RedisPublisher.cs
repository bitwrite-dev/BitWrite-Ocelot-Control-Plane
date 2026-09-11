using BitWrite.OcelotControl.Application.Interfaces;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace BitWrite.OcelotControl.Infrastructure.Redis;

public class RedisPublisher : IRedisPublisher
{
    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly ILogger<RedisPublisher> _logger;

    public RedisPublisher(IConnectionMultiplexer connectionMultiplexer, ILogger<RedisPublisher> logger)
    {
        _connectionMultiplexer = connectionMultiplexer;
        _logger = logger;
    }

    public async Task PublishAsync(string channel, string message, CancellationToken cancellationToken = default)
    {
        var subscriber = _connectionMultiplexer.GetSubscriber();
        await subscriber.PublishAsync(channel, message);
        _logger.LogDebug("Published to channel {Channel}: {Message}", channel, message);
    }

    public async Task PublishAsync<T>(string channel, T message, CancellationToken cancellationToken = default) where T : class
    {
        var json = System.Text.Json.JsonSerializer.Serialize(message);
        await PublishAsync(channel, json, cancellationToken);
    }
}