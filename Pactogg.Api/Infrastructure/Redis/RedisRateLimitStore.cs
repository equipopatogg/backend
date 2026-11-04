using System.Collections.Concurrent;
using StackExchange.Redis;

namespace Pactogg.Api.Infrastructure.Redis;

public sealed class RedisRateLimitStore : IRateLimitStore
{
    private const string Script = """
        local current = redis.call('INCR', KEYS[1])
        if current == 1 then
            redis.call('PEXPIRE', KEYS[1], ARGV[1])
        end
        return current
        """;

    private readonly RedisConnection _redis;
    private readonly ConcurrentDictionary<string, (long Count, DateTime ResetAt)> _fallback = new();

    public RedisRateLimitStore(RedisConnection redis)
    {
        _redis = redis;
    }

    public async Task<long> IncrementAsync(string key, TimeSpan window, CancellationToken cancellationToken)
    {
        var fullKey = _redis.Key($"rl:{key}");
        var db = _redis.TryGetDatabase();
        if (db is not null)
        {
            try
            {
                var result = await db.ScriptEvaluateAsync(
                    Script,
                    [fullKey],
                    [(long)window.TotalMilliseconds]);
                return (long)result;
            }
            catch (RedisException)
            {
            }
        }

        var now = DateTime.UtcNow;
        return _fallback.AddOrUpdate(
            fullKey,
            _ => (1, now.Add(window)),
            (_, current) => current.ResetAt <= now ? (1, now.Add(window)) : (current.Count + 1, current.ResetAt)).Count;
    }
}
