using System.Security.Cryptography;
using System.Text;
using MongoDB.Bson;
using Pactogg.Api.Infrastructure.Redis;

namespace Pactogg.Api.Infrastructure.Security;

/// <summary>
/// Tokens de un solo uso para verificar correo y restablecer contraseña (CLARIFICACION seguridad:
/// "token de un solo uso, hasheado y con caducidad"). En Redis solo se guarda el SHA-256 del token.
/// </summary>
public sealed class OneTimeTokenStore
{
    private readonly ICacheService _cache;

    public OneTimeTokenStore(ICacheService cache)
    {
        _cache = cache;
    }

    public async Task<string> IssueAsync(string purpose, ObjectId userId, TimeSpan ttl, CancellationToken cancellationToken)
    {
        var token = Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        await _cache.SetAsync(Key(purpose, token), userId.ToString(), ttl, cancellationToken);
        return token;
    }

    /// <summary>Consume el token: devuelve el usuario y lo invalida. Null si no existe, caducó o ya se usó.</summary>
    public async Task<ObjectId?> ConsumeAsync(string purpose, string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 128)
        {
            return null;
        }

        var key = Key(purpose, token);
        var value = await _cache.GetAsync(key, cancellationToken);
        if (value is null || !ObjectId.TryParse(value, out var userId))
        {
            return null;
        }

        // Marca de uso atómica: si dos solicitudes llegan a la vez solo una gana.
        if (!await _cache.TrySetIfAbsentAsync(key + ":used", "1", TimeSpan.FromHours(25), cancellationToken))
        {
            return null;
        }

        await _cache.RemoveAsync(key, cancellationToken);
        return userId;
    }

    private static string Key(string purpose, string token) =>
        $"ott:{purpose}:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)))}";

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
