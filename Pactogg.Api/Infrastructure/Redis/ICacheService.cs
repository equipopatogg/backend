namespace Pactogg.Api.Infrastructure.Redis;

public interface ICacheService
{
    Task<string?> GetAsync(string key, CancellationToken cancellationToken);
    Task SetAsync(string key, string value, TimeSpan ttl, CancellationToken cancellationToken);
    Task RemoveAsync(string key, CancellationToken cancellationToken);

    /// <summary>SET NX: escribe solo si la clave no existe. true si la escribió.</summary>
    Task<bool> TrySetIfAbsentAsync(string key, string value, TimeSpan ttl, CancellationToken cancellationToken);
}
