using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using MongoDB.Bson;
using Pactogg.Api.Common;
using Pactogg.Api.Common.Extensions;
using Pactogg.Api.Infrastructure.Audit;
using Pactogg.Api.Infrastructure.Redis;
using Pactogg.Api.Infrastructure.Security;
using Pactogg.Api.Infrastructure.Storage;
using Pactogg.Api.Modules.Auth.Abstractions;
using Pactogg.Api.Modules.Commitments.Abstractions;
using Pactogg.Api.Modules.Devices;
using Pactogg.Api.Modules.Evidence.Abstractions;
using Pactogg.Api.Modules.Groups;
using Pactogg.Api.Modules.Notifications.Abstractions;
using Pactogg.Api.Modules.Reports;
using Pactogg.Api.Modules.Votes.Abstractions;

namespace Pactogg.Api.Modules.Users;

/// <summary>USR-04: confirmación para borrar la cuenta (USR04-D2).</summary>
public sealed class DeleteAccountRequest
{
    /// <summary>Obligatoria si la cuenta tiene contraseña.</summary>
    [StringLength(128)]
    public string? Password { get; set; }

    [Required, RegularExpression("^ELIMINAR$", ErrorMessage = "Escribe ELIMINAR para confirmar.")]
    public string Confirm { get; set; } = string.Empty;
}

/// <summary>USR-04 (CLARIFICACION #12, #15, #16; constitución §6.1): borra o anonimiza todo lo personal de inmediato.</summary>
public sealed class AccountDeletionService
{
    private readonly IUserRepository _users;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly ITokenRevocationStore _revocations;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IDeviceRepository _devices;
    private readonly INotificationRepository _notifications;
    private readonly IEvidenceRepository _evidence;
    private readonly IObjectStorage _storage;
    private readonly IVoteRepository _votes;
    private readonly ICommitmentRepository _commitments;
    private readonly IBlockRepository _blocks;
    private readonly GroupService _groups;
    private readonly IAuditService _audit;
    private readonly ICacheService _cache;
    private readonly ILogger<AccountDeletionService> _logger;

    public AccountDeletionService(IUserRepository users, IRefreshTokenRepository refreshTokens, ITokenRevocationStore revocations, IPasswordHasher passwordHasher,
        IDeviceRepository devices, INotificationRepository notifications, IEvidenceRepository evidence, IObjectStorage storage, IVoteRepository votes,
        ICommitmentRepository commitments, IBlockRepository blocks, GroupService groups, IAuditService audit, ICacheService cache, ILogger<AccountDeletionService> logger)
    {
        _users = users;
        _refreshTokens = refreshTokens;
        _revocations = revocations;
        _passwordHasher = passwordHasher;
        _devices = devices;
        _notifications = notifications;
        _evidence = evidence;
        _storage = storage;
        _votes = votes;
        _commitments = commitments;
        _blocks = blocks;
        _groups = groups;
        _audit = audit;
        _cache = cache;
        _logger = logger;
    }

    public async Task DeleteAsync(ClaimsPrincipal principal, DeleteAccountRequest request, CancellationToken cancellationToken)
    {
        var userId = principal.GetRequiredObjectId();
        var user = await _users.GetByIdAsync(userId, cancellationToken) ?? throw ApiException.Unauthorized();
        if (!string.IsNullOrEmpty(user.PasswordHash)
            && (string.IsNullOrEmpty(request.Password) || !_passwordHasher.Verify(user.PasswordHash, request.Password)))
        {
            throw ApiException.Unprocessable(ErrorCodes.InvalidCredentials, "La contraseña no es correcta.");
        }

        var now = DateTime.UtcNow;

        // 1. Grupos: retos activos cancelados con devolución de fianzas y salida de cada grupo.
        await _groups.RemoveForAccountDeletionAsync(userId, cancellationToken);

        // 2. Fotos de evidencia fuera de S3/CloudFront.
        foreach (var photo in await _evidence.ListPhotosByUserAsync(userId, cancellationToken))
        {
            await _evidence.MarkRemovedAsync(photo.Id, now, cancellationToken);
            if (photo.ObjectKey is string key)
            {
                try
                {
                    await _storage.DeleteAsync(key, cancellationToken);
                }
                catch (ApiException)
                {
                    // La foto ya no se muestra (removed); el borrado del objeto se reintenta con la regla de 30 días del bucket.
                    _logger.LogWarning("No se pudo borrar un objeto de evidencia al eliminar una cuenta.");
                }
            }
        }

        // 3. Comentarios de voto, compromisos, avisos, bloqueos y dispositivos.
        await _votes.ClearCommentsByJurorAsync(userId, cancellationToken);
        await _commitments.CloseOpenForUserAsync(userId, now, cancellationToken);
        await _notifications.DeleteAllForUserAsync(userId, cancellationToken);
        await _blocks.DeleteAllForUserAsync(userId, cancellationToken);
        await _devices.DeleteAsync(userId, null, cancellationToken);

        // 4. Sesiones y datos personales.
        await _refreshTokens.RevokeAllForUserAsync(userId, now, cancellationToken);
        var jti = principal.FindFirstValue("jti");
        if (!string.IsNullOrWhiteSpace(jti) && long.TryParse(principal.FindFirstValue("exp"), out var exp))
        {
            await _revocations.RevokeAsync(jti, DateTimeOffset.FromUnixTimeSeconds(exp).UtcDateTime, cancellationToken);
        }

        await _users.AnonymizeAsync(userId, now, cancellationToken);
        await _cache.BumpVersionAsync(CacheExtensions.UserScope(userId), cancellationToken);
        // La auditoría solo guarda identificadores; al anonimizar al usuario queda anónima (CLARIFICACION #16, #18).
        await _audit.AppendAsync("user.deleted", userId, "user", userId, null, new { }, cancellationToken);
    }
}
