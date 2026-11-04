using StackExchange.Redis;
using Pactogg.Api.Infrastructure.Redis;

namespace Pactogg.Api.Infrastructure.Security;

public sealed class RedisLoginAttemptStore : ILoginAttemptStore
{
    private readonly RedisConnection _redis;

    public RedisLoginAttemptStore(RedisConnection redis)
    {
        _redis = redis;
    }

    public async Task<int> GetFailuresAsync(string key, CancellationToken cancellationToken)
    {
        var db = _redis.TryGetDatabase();
        if (db is null)
        {
            return 0;
        }

        try
        {
            var value = await db.StringGetAsync(_redis.Key($"login:{key}"));
            return value.HasValue && int.TryParse(value.ToString(), out var count) ? count : 0;
        }
        catch (RedisException)
        {
            return 0;
        }
    }

    public async Task RecordFailureAsync(string key, TimeSpan ttl, CancellationToken cancellationToken)
    {
        var db = _redis.TryGetDatabase();
        if (db is null)
        {
            return;
        }

        try
        {
            var fullKey = _redis.Key($"login:{key}");
            await db.StringIncrementAsync(fullKey);
            await db.KeyExpireAsync(fullKey, ttl);
        }
        catch (RedisException)
        {
        }
    }

    public async Task ClearAsync(string key, CancellationToken cancellationToken)
    {
        var db = _redis.TryGetDatabase();
        if (db is null)
        {
            return;
        }

        try
        {
            await db.KeyDeleteAsync(_redis.Key($"login:{key}"));
        }
        catch (RedisException)
        {
        }
    }
}
