using BitWrite.OcelotControl.Application.Interfaces;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace BitWrite.OcelotControl.Infrastructure.Redis;

public class RedisDistributedLock : IDistributedLock
{
    private readonly IConnectionMultiplexer _connectionMultiplexer;
    private readonly ILogger<RedisDistributedLock> _logger;

    public RedisDistributedLock(IConnectionMultiplexer connectionMultiplexer, ILogger<RedisDistributedLock> logger)
    {
        _connectionMultiplexer = connectionMultiplexer;
        _logger = logger;
    }

    public async Task<LockResult> AcquireAsync(
        string resource,
        TimeSpan expiry,
        TimeSpan? waitTimeout = null,
        TimeSpan? retryInterval = null,
        CancellationToken cancellationToken = default)
    {
        var db = _connectionMultiplexer.GetDatabase();
        var lockKey = $"lock:{resource}";
        var lockId = Guid.NewGuid().ToString();
        var timeout = waitTimeout ?? TimeSpan.FromSeconds(30);
        var interval = retryInterval ?? TimeSpan.FromMilliseconds(100);
        var endTime = DateTime.UtcNow.Add(timeout);

        while (DateTime.UtcNow < endTime)
        {
            var acquired = await db.StringSetAsync(lockKey, lockId, expiry, When.NotExists);
            
            if (acquired)
            {
                _logger.LogDebug("Acquired lock for resource: {Resource}", resource);
                return new LockResult(true, lockId, null);
            }

            _logger.LogDebug("Waiting for lock on resource: {Resource}", resource);
            await Task.Delay(interval, cancellationToken);
        }

        _logger.LogWarning("Failed to acquire lock for resource: {Resource}", resource);
        return new LockResult(false, null, "Lock acquisition timeout");
    }

    public async Task ReleaseAsync(string resource, string lockId, CancellationToken cancellationToken = default)
    {
        var db = _connectionMultiplexer.GetDatabase();
        var lockKey = $"lock:{resource}";
        
        // Use Lua script to ensure atomic check-and-delete
        var script = @"
            if redis.call('get', KEYS[1]) == ARGV[1] then
                return redis.call('del', KEYS[1])
            else
                return 0
            end";
        
        var result = await db.ScriptEvaluateAsync(script, new RedisKey[] { lockKey }, new RedisValue[] { lockId });
        
        if ((int)result == 1)
        {
            _logger.LogDebug("Released lock for resource: {Resource}", resource);
        }
        else
        {
            _logger.LogWarning("Failed to release lock for resource: {Resource} (lock not owned or expired)", resource);
        }
    }
}