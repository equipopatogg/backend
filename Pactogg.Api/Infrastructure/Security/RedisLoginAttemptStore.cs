using StackExchange.Redis;
using Pactogg.Api.Infrastructure.Redis;

namespace Pactogg.Api.Infrastructure.Security;

/// <summary>
/// Fallos de login por correo e IP. Si Redis no está disponible se usa un contador local en memoria
/// (constitución §8: límites locales de respaldo), para que el bloqueo tras 5 fallos nunca se desactive.
/// </summary>
public sealed class RedisLoginAttemptStore : ILoginAttemptStore
{
    private static readonly object FallbackLock = new();
    private readonly RedisConnection _redis;

    public RedisLoginAttemptStore(RedisConnection redis)
    {
        _redis = redis;
    }

    public async Task<int> GetFailuresAsync(string key, CancellationToken cancellationToken)
    {
        var fullKey = _redis.Key($"login:{key}");
        var db = _redis.TryGetDatabase();
        if (db is not null)
        {
            try
            {
                var value = await db.StringGetAsync(fullKey);
                return value.HasValue && int.TryParse(value.ToString(), out var count) ? count : 0;
            }
            catch (RedisException)
            {
            }
        }

        return ReadFallback(fullKey);
    }

    public async Task RecordFailureAsync(string key, TimeSpan ttl, CancellationToken cancellationToken)
    {
        var fullKey = _redis.Key($"login:{key}");
        var db = _redis.TryGetDatabase();
        if (db is not null)
        {
            try
            {
                await db.StringIncrementAsync(fullKey);
                await db.KeyExpireAsync(fullKey, ttl);
                return;
            }
            catch (RedisException)
            {
            }
        }

        lock (FallbackLock)
        {
            _redis.SetFallback(fullKey, (ReadFallback(fullKey) + 1).ToString(), ttl);
        }
    }

    public async Task ClearAsync(string key, CancellationToken cancellationToken)
    {
        var fullKey = _redis.Key($"login:{key}");
        _redis.RemoveFallback(fullKey);
        var db = _redis.TryGetDatabase();
        if (db is null)
        {
            return;
        }

        try
        {
            await db.KeyDeleteAsync(fullKey);
        }
        catch (RedisException)
        {
        }
    }

    private int ReadFallback(string fullKey) =>
        int.TryParse(_redis.GetFallback(fullKey), out var count) ? count : 0;
}
