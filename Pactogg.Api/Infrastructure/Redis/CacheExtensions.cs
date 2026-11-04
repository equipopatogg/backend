using System.Text.Json;
using System.Text.Json.Serialization;
using Pactogg.Api.Common;

namespace Pactogg.Api.Infrastructure.Redis;

/// <summary>
/// Caché por versiones: cada ámbito (usuario o grupo) tiene una versión en Redis.
/// Las llaves de lectura incluyen la versión; invalidar = cambiar la versión (no hace falta borrar por patrón).
/// </summary>
public static class CacheExtensions
{
    private static readonly TimeSpan VersionTtl = TimeSpan.FromHours(6);

    public static string UserScope(object userId) => $"user:{userId}";
    public static string GroupScope(object groupId) => $"group:{groupId}";

    public static async Task<string> GetVersionAsync(this ICacheService cache, string scope, CancellationToken cancellationToken)
    {
        var key = $"ver:{scope}";
        var version = await cache.GetAsync(key, cancellationToken);
        if (!string.IsNullOrWhiteSpace(version))
        {
            return version;
        }

        version = NewVersion();
        await cache.SetAsync(key, version, VersionTtl, cancellationToken);
        return version;
    }

    public static Task BumpVersionAsync(this ICacheService cache, string scope, CancellationToken cancellationToken) =>
        cache.SetAsync($"ver:{scope}", NewVersion(), VersionTtl, cancellationToken);

    public static async Task<T> GetOrCreateJsonAsync<T>(
        this ICacheService cache,
        string key,
        TimeSpan ttl,
        Func<Task<T>> factory,
        CancellationToken cancellationToken)
    {
        var cached = await cache.GetAsync(key, cancellationToken);
        if (!string.IsNullOrWhiteSpace(cached))
        {
            try
            {
                var value = JsonSerializer.Deserialize<T>(cached, ApiResults.JsonOptions);
                if (value is not null)
                {
                    return value;
                }
            }
            catch (JsonException)
            {
            }
        }

        var created = await factory();
        await cache.SetAsync(key, JsonSerializer.Serialize(created, ApiResults.JsonOptions), ttl, cancellationToken);
        return created;
    }

    private static string NewVersion() => Guid.NewGuid().ToString("N")[..12];
}

public sealed record PageSnapshot<T>(IReadOnlyList<T> Items, string? NextCursor, bool HasMore)
{
    [JsonIgnore]
    public object Meta => new { next_cursor = NextCursor, has_more = HasMore };
}
