using StackExchange.Redis;

namespace BitWrite.OcelotControl.Infrastructure.Repositories;

public abstract class RedisRepositoryBase
{
    protected readonly IDatabase Database;
    protected readonly IConnectionMultiplexer ConnectionMultiplexer;

    protected RedisRepositoryBase(IConnectionMultiplexer connectionMultiplexer)
    {
        ConnectionMultiplexer = connectionMultiplexer;
        Database = connectionMultiplexer.GetDatabase();
    }

    protected async Task<bool> SetHashAsync(string key, HashEntry[] entries, TimeSpan? expiry = null)
    {
        if (entries.Length == 0) return true;
        
        await Database.HashSetAsync(key, entries);
        
        if (expiry.HasValue)
        {
            await Database.KeyExpireAsync(key, expiry);
        }
        
        return true;
    }

    protected async Task<HashEntry[]> GetHashAsync(string key)
    {
        return await Database.HashGetAllAsync(key);
    }

    protected async Task<bool> HashExistsAsync(string key)
    {
        return await Database.KeyExistsAsync(key);
    }

    protected async Task DeleteAsync(string key)
    {
        await Database.KeyDeleteAsync(key);
    }

    protected async Task<long> SetAddAsync(string setKey, params RedisValue[] values)
    {
        return await Database.SetAddAsync(setKey, values);
    }

    protected async Task<long> SetRemoveAsync(string setKey, params RedisValue[] values)
    {
        return await Database.SetRemoveAsync(setKey, values);
    }

    protected async Task<RedisValue[]> SetMembersAsync(string setKey)
    {
        return await Database.SetMembersAsync(setKey);
    }

    protected async Task<bool> SetContainsAsync(string setKey, RedisValue value)
    {
        return await Database.SetContainsAsync(setKey, value);
    }

    protected async Task<bool> SortedSetAddAsync(string key, RedisValue value, double score)
    {
        return await Database.SortedSetAddAsync(key, value, score);
    }

    protected async Task<bool> SortedSetRemoveAsync(string key, RedisValue value)
    {
        return await Database.SortedSetRemoveAsync(key, value);
    }

    protected async Task<RedisValue[]> SortedSetRangeByScoreAsync(
        string key, 
        double start = double.NegativeInfinity, 
        double stop = double.PositiveInfinity, 
        long skip = 0, 
        long take = 100)
    {
        return await Database.SortedSetRangeByScoreAsync(key, start, stop, Exclude.None, Order.Ascending, skip, take);
    }

    protected async Task<string?> StringGetAsync(string key)
    {
        return await Database.StringGetAsync(key);
    }

    protected async Task<bool> StringSetAsync(string key, string value, TimeSpan? expiry = null)
    {
        return await Database.StringSetAsync(key, value, expiry);
    }

    protected static string GetEntry(HashEntry[] entries, string name)
    {
        var entry = entries.FirstOrDefault(e => e.Name == name);
        return entry.Value.IsNullOrEmpty ? "" : entry.Value.ToString();
    }
}