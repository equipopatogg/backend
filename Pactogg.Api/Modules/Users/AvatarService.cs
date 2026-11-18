using Microsoft.Extensions.Options;
using MongoDB.Bson;
using Pactogg.Api.Common;
using Pactogg.Api.Infrastructure.Audit;
using Pactogg.Api.Infrastructure.Moderation;
using Pactogg.Api.Infrastructure.Options;
using Pactogg.Api.Infrastructure.Redis;
using Pactogg.Api.Infrastructure.Storage;
using Pactogg.Api.Modules.Auth.Abstractions;
using Pactogg.Api.Modules.Auth.Domain;
using Pactogg.Api.Modules.Evidence;
using Pactogg.Api.Modules.Groups.Abstractions;

namespace Pactogg.Api.Modules.Users;

/// <summary>
/// USR-07 (2026-11-17, pedido del PO): foto de perfil. Misma ruta que las evidencias: tipo y tamaño, se vuelve a
/// codificar como JPEG sin metadatos, Rekognition la revisa (falla cerrada) y solo entonces se guarda en S3,
/// bajo avatars/, que no tiene la regla de borrado de 30 días.
/// </summary>
public sealed class AvatarService
{
    public const string PhotoAvatarId = "photo";
    private const long MaxAvatarBytes = 5 * 1024 * 1024;

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp"
    };

    private readonly IUserRepository _users;
    private readonly IGroupMemberRepository _members;
    private readonly IModerationService _moderation;
    private readonly IObjectStorage _storage;
    private readonly ICacheService _cache;
    private readonly IAuditService _audit;
    private readonly long _maxBytes;

    public AvatarService(
        IUserRepository users,
        IGroupMemberRepository members,
        IModerationService moderation,
        IObjectStorage storage,
        ICacheService cache,
        IAuditService audit,
        IOptions<EvidenceOptions> evidence)
    {
        _users = users;
        _members = members;
        _moderation = moderation;
        _storage = storage;
        _cache = cache;
        _audit = audit;
        _maxBytes = Math.Min(MaxAvatarBytes, evidence.Value.MaxImageBytes);
    }

    public long MaxBytes => _maxBytes;

    public async Task<User> UploadAsync(ObjectId userId, byte[] content, string? contentType, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(contentType) || !AllowedContentTypes.Contains(contentType))
        {
            throw ApiException.Unprocessable(ErrorCodes.UnsupportedMediaType, "Solo se admiten JPEG, PNG o WebP.");
        }

        if (content.LongLength <= 0 || content.LongLength > _maxBytes)
        {
            throw ApiException.Unprocessable(ErrorCodes.ImageTooLarge, "La imagen supera el tamaño permitido.");
        }

        var user = await _users.GetByIdAsync(userId, cancellationToken) ?? throw ApiException.Unauthorized();
        // Se copia antes de actualizar: el repositorio puede devolver el mismo objeto ya modificado.
        var oldKey = user.AvatarKey;
        var normalized = ImageProcessor.Normalize(content);
        var moderation = await _moderation.ModerateImageAsync(normalized, cancellationToken);
        if (!moderation.Approved)
        {
            throw ApiException.Unprocessable(ErrorCodes.ContentRejected, "La imagen fue rechazada por moderación.", new { labels = moderation.Labels });
        }

        var key = $"avatars/{userId}/{ObjectId.GenerateNewId()}.jpg";
        await _storage.PutAsync(normalized, "image/jpeg", key, cancellationToken);
        // Con CloudFront la URL es estable; sin CDN es una URL firmada de 7 días (máximo de S3).
        var url = await _storage.GetReadUrlAsync(key, TimeSpan.FromDays(7), cancellationToken);
        if (string.IsNullOrWhiteSpace(url))
        {
            await TryDeleteAsync(key);
            throw ApiException.ServiceUnavailable(ErrorCodes.StorageUnavailable, "No se pudo guardar la foto.");
        }

        var updated = await _users.SetAvatarPhotoAsync(userId, key, url, DateTime.UtcNow, cancellationToken)
            ?? throw ApiException.Unauthorized();
        if (oldKey is not null && oldKey != key)
        {
            await TryDeleteAsync(oldKey);
        }

        await _cache.BumpVersionAsync(CacheExtensions.UserScope(userId), cancellationToken);
        foreach (var groupId in await _members.ListActiveGroupIdsAsync(userId, cancellationToken))
        {
            await _cache.BumpVersionAsync(CacheExtensions.GroupScope(groupId), cancellationToken);
        }

        await _audit.AppendAsync("user.avatar_photo", userId, "user", userId, null, new { replaced = oldKey is not null }, cancellationToken);
        return updated;
    }

    /// <summary>Borrado de la foto anterior. Si falla no se detiene la operación del usuario.</summary>
    public async Task TryDeleteAsync(string key)
    {
        try
        {
            await _storage.DeleteAsync(key, CancellationToken.None);
        }
        catch (Exception)
        {
            // Queda huérfana en avatars/; no afecta al usuario y se puede limpiar después.
        }
    }
}
