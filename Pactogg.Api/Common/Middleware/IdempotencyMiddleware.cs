using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Pactogg.Api.Infrastructure.Redis;

namespace Pactogg.Api.Common.Middleware;

/// <summary>Marca un endpoint que exige la cabecera Idempotency-Key (DESC_BACK 9, CLARIFICACION seguridad API).</summary>
public sealed class RequireIdempotencyKeyMetadata
{
    public static readonly RequireIdempotencyKeyMetadata Instance = new();
}

public static class IdempotencyEndpointExtensions
{
    public static TBuilder RequireIdempotencyKey<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder.WithMetadata(RequireIdempotencyKeyMetadata.Instance);
}

/// <summary>
/// Idempotency-Key obligatoria en aceptar reto y votar. La primera respuesta 2xx se guarda 24 h en Redis y
/// se repite tal cual ante el mismo usuario + ruta + clave; mientras se procesa, un segundo intento recibe 409.
/// Si la misma clave llega con otro cuerpo se responde 422.
/// </summary>
public sealed partial class IdempotencyMiddleware
{
    public const string HeaderName = "Idempotency-Key";
    private static readonly TimeSpan StoredTtl = TimeSpan.FromHours(24);
    private static readonly TimeSpan LockTtl = TimeSpan.FromSeconds(60);
    private const int MaxStoredBody = 64 * 1024;
    private const string InProgress = "in_progress";

    private readonly RequestDelegate _next;

    public IdempotencyMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ICacheService cache)
    {
        if (context.GetEndpoint()?.Metadata.GetMetadata<RequireIdempotencyKeyMetadata>() is null)
        {
            await _next(context);
            return;
        }

        var userId = context.User.FindFirstValue("sub") ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            // Sin usuario la autorización ya respondió 401; no hay nada que deduplicar.
            await _next(context);
            return;
        }

        var key = context.Request.Headers[HeaderName].ToString();
        if (string.IsNullOrWhiteSpace(key) || !KeyPattern().IsMatch(key))
        {
            throw ApiException.BadRequest(
                ErrorCodes.IdempotencyKeyRequired,
                "La cabecera Idempotency-Key es obligatoria (8 a 128 caracteres: letras, números, guion o guion bajo).");
        }

        var requestHash = await HashBodyAsync(context.Request);
        var cacheKey = $"idem:{userId}:{context.Request.Method}:{context.Request.Path.Value?.ToLowerInvariant()}:{key}";

        if (!await cache.TrySetIfAbsentAsync(cacheKey, InProgress, LockTtl, context.RequestAborted))
        {
            var existing = await cache.GetAsync(cacheKey, context.RequestAborted);
            if (existing is null || existing == InProgress)
            {
                throw ApiException.Conflict(ErrorCodes.IdempotencyKeyInUse, "Hay una solicitud con la misma Idempotency-Key en proceso.");
            }

            var stored = JsonSerializer.Deserialize<StoredResponse>(existing);
            if (stored is null)
            {
                throw ApiException.Conflict(ErrorCodes.IdempotencyKeyInUse, "Hay una solicitud con la misma Idempotency-Key en proceso.");
            }

            if (stored.RequestHash != requestHash)
            {
                throw ApiException.Unprocessable(ErrorCodes.IdempotencyKeyInUse, "La Idempotency-Key ya se usó con otro cuerpo.");
            }

            context.Response.StatusCode = stored.Status;
            context.Response.ContentType = stored.ContentType;
            context.Response.Headers["Idempotent-Replayed"] = "true";
            await context.Response.WriteAsync(stored.Body, context.RequestAborted);
            return;
        }

        var original = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;
        var stored2xx = false;
        try
        {
            await _next(context);

            buffer.Position = 0;
            var body = await new StreamReader(buffer, Encoding.UTF8).ReadToEndAsync(context.RequestAborted);
            if (context.Response.StatusCode is >= 200 and < 300 && body.Length <= MaxStoredBody)
            {
                var record = new StoredResponse(context.Response.StatusCode, context.Response.ContentType ?? "application/json", body, requestHash);
                await cache.SetAsync(cacheKey, JsonSerializer.Serialize(record), StoredTtl, CancellationToken.None);
                stored2xx = true;
            }

            buffer.Position = 0;
            context.Response.Body = original;
            await buffer.CopyToAsync(original, context.RequestAborted);
        }
        finally
        {
            context.Response.Body = original;
            if (!stored2xx)
            {
                // Un error no consume la clave: el cliente puede reintentar con la misma.
                await cache.RemoveAsync(cacheKey, CancellationToken.None);
            }
        }
    }

    private static async Task<string> HashBodyAsync(HttpRequest request)
    {
        request.EnableBuffering();
        request.Body.Position = 0;
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(request.Body, request.HttpContext.RequestAborted);
        request.Body.Position = 0;
        return Convert.ToHexString(hash);
    }

    [GeneratedRegex("^[A-Za-z0-9_-]{8,128}$")]
    private static partial Regex KeyPattern();

    private sealed record StoredResponse(int Status, string ContentType, string Body, string RequestHash);
}
