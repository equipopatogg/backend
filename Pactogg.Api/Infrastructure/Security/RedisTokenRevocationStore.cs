using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Pactogg.Api.Infrastructure.Options;
using Pactogg.Api.Infrastructure.Redis;

namespace Pactogg.Api.Infrastructure.Security;

public sealed class RedisTokenRevocationStore : ITokenRevocationStore
{
    private readonly RedisConnection _redis;
    private readonly ConcurrentDictionary<string, DateTime> _fallback = new();
    private readonly bool _required;

    public RedisTokenRevocationStore(RedisConnection redis, IOptions<RedisOptions> options)
    {
        _redis = redis;
        _required = options.Value.Required;
    }

    public async Task RevokeAsync(string jti, DateTime expiresAt, CancellationToken cancellationToken)
    {
        var ttl = expiresAt - DateTime.UtcNow;
        if (ttl <= TimeSpan.Zero)
        {
            return;
        }

        var key = _redis.Key($"revoked:{jti}");
        var db = _redis.TryGetDatabase();
        if (db is not null)
        {
            try
            {
                await db.StringSetAsync(key, "1", ttl);
                return;
            }
            catch (RedisException)
            {
            }
        }

        _fallback[jti] = expiresAt;
    }

    public async Task<bool> IsRevokedAsync(string jti, CancellationToken cancellationToken)
    {
        var db = _redis.TryGetDatabase();
        if (db is not null)
        {
            try
            {
                return await db.KeyExistsAsync(_redis.Key($"revoked:{jti}"));
            }
            catch (RedisException)
            {
            }
        }

        // Falla cerrada: sin Redis no se puede saber si otra instancia revocó el token.
        if (_required)
        {
            return true;
        }

        if (_fallback.TryGetValue(jti, out var expiresAt))
        {
            if (expiresAt > DateTime.UtcNow)
            {
                return true;
            }

            _fallback.TryRemove(jti, out _);
        }

        return false;
    }
}
