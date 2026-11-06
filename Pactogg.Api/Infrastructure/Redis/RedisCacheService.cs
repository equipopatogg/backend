using StackExchange.Redis;

namespace Pactogg.Api.Infrastructure.Redis;

public sealed class RedisCacheService : ICacheService
{
    private readonly RedisConnection _redis;

    public RedisCacheService(RedisConnection redis)
    {
        _redis = redis;
    }

    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken)
    {
        var db = _redis.TryGetDatabase();
        if (db is null)
        {
            return _redis.GetFallback(_redis.Key(key));
        }

        try
        {
            var value = await db.StringGetAsync(_redis.Key(key));
            return value.HasValue ? value.ToString() : null;
        }
        catch (RedisException)
        {
            return _redis.GetFallback(_redis.Key(key));
        }
    }

    public async Task SetAsync(string key, string value, TimeSpan ttl, CancellationToken cancellationToken)
    {
        var db = _redis.TryGetDatabase();
        if (db is null)
        {
            _redis.SetFallback(_redis.Key(key), value, ttl);
            return;
        }

        try
        {
            await db.StringSetAsync(_redis.Key(key), value, ttl);
        }
        catch (RedisException)
        {
            _redis.SetFallback(_redis.Key(key), value, ttl);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken)
    {
        var db = _redis.TryGetDatabase();
        if (db is null)
        {
            _redis.RemoveFallback(_redis.Key(key));
            return;
        }

        try
        {
            await db.KeyDeleteAsync(_redis.Key(key));
        }
        catch (RedisException)
        {
            _redis.RemoveFallback(_redis.Key(key));
        }
    }

    public async Task<bool> TrySetIfAbsentAsync(string key, string value, TimeSpan ttl, CancellationToken cancellationToken)
    {
        var db = _redis.TryGetDatabase();
        if (db is null)
        {
            return _redis.TryAddFallback(_redis.Key(key), value, ttl);
        }

        try
        {
            return await db.StringSetAsync(_redis.Key(key), value, ttl, When.NotExists);
        }
        catch (RedisException)
        {
            return _redis.TryAddFallback(_redis.Key(key), value, ttl);
        }
    }
}
