using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Pactogg.Api.Infrastructure.Options;

namespace Pactogg.Api.Infrastructure.Redis;

public sealed class RedisConnection : IDisposable
{
    private readonly ILogger<RedisConnection> _logger;
    private readonly Lazy<IConnectionMultiplexer?> _multiplexer;
    private readonly ConcurrentDictionary<string, (string Value, DateTime ExpiresAt)> _fallback = new();

    public RedisConnection(IOptions<RedisOptions> options, ILogger<RedisConnection> logger)
    {
        _logger = logger;
        var connectionString = options.Value.ConnectionString;
        _multiplexer = new Lazy<IConnectionMultiplexer?>(() =>
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                _logger.LogWarning("Redis no configurado; se usará almacenamiento local en memoria.");
                return null;
            }

            try
            {
                var config = ConfigurationOptions.Parse(connectionString);
                config.AbortOnConnectFail = false;
                config.ConnectRetry = 2;
                config.ConnectTimeout = 2000;
                config.SyncTimeout = 2000;
                return ConnectionMultiplexer.Connect(config);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo conectar a Redis; se usará almacenamiento local en memoria.");
                return null;
            }
        }, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public IDatabase? TryGetDatabase()
    {
        try
        {
            var multiplexer = _multiplexer.Value;
            return multiplexer?.IsConnected == true ? multiplexer.GetDatabase() : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis no disponible en esta operación.");
            return null;
        }
    }

    public string? GetFallback(string key)
    {
        if (!_fallback.TryGetValue(key, out var entry))
        {
            return null;
        }

        if (entry.ExpiresAt <= DateTime.UtcNow)
        {
            _fallback.TryRemove(key, out _);
            return null;
        }

        return entry.Value;
    }

    public void SetFallback(string key, string value, TimeSpan ttl) =>
        _fallback[key] = (value, DateTime.UtcNow.Add(ttl));

    public void RemoveFallback(string key) => _fallback.TryRemove(key, out _);

    public bool TryAddFallback(string key, string value, TimeSpan ttl)
    {
        var expiresAt = DateTime.UtcNow.Add(ttl);
        while (true)
        {
            if (_fallback.TryAdd(key, (value, expiresAt)))
            {
                return true;
            }

            if (!_fallback.TryGetValue(key, out var current))
            {
                continue;
            }

            if (current.ExpiresAt > DateTime.UtcNow)
            {
                return false;
            }

            if (_fallback.TryUpdate(key, (value, expiresAt), current))
            {
                return true;
            }
        }
    }

    public void Dispose()
    {
        if (_multiplexer.IsValueCreated)
        {
            _multiplexer.Value?.Dispose();
        }
    }

    public string Key(string name) => $"pactogg:{name}";
}
