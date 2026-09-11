using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace BitWrite.OcelotControl.Infrastructure.Redis;

public class RedisHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer _connectionMultiplexer;

    public RedisHealthCheck(IConnectionMultiplexer connectionMultiplexer)
    {
        _connectionMultiplexer = connectionMultiplexer;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _connectionMultiplexer.GetDatabase();
            var ping = await db.PingAsync();
            
            return HealthCheckResult.Healthy($"Redis ping: {ping.TotalMilliseconds}ms");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Redis connection failed", ex);
        }
    }
}