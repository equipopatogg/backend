using System.Text.RegularExpressions;
using MongoDB.Bson;
using Pactogg.Api.Common;
using Pactogg.Api.Infrastructure.Audit;
using Pactogg.Api.Infrastructure.Moderation;
using Pactogg.Api.Infrastructure.Redis;
using Pactogg.Api.Modules.Auth.Abstractions;
using Pactogg.Api.Modules.Auth.Domain;
using Pactogg.Api.Modules.Groups.Abstractions;
using Pactogg.Api.Modules.Users.Dtos;

namespace Pactogg.Api.Modules.Users;

public sealed partial class UserService
{
    private static readonly TimeSpan ProfileTtl = TimeSpan.FromSeconds(60);

    private readonly IUserRepository _users;
    private readonly IGroupMemberRepository _members;
    private readonly IModerationService _moderation;
    private readonly ICacheService _cache;
    private readonly IAuditService _audit;

    public UserService(
        IUserRepository users,
        IGroupMemberRepository members,
        IModerationService moderation,
        ICacheService cache,
        IAuditService audit)
    {
        _users = users;
        _members = members;
        _moderation = moderation;
        _cache = cache;
        _audit = audit;
    }

    [GeneratedRegex("^[A-Za-z0-9_.-]{3,24}$")]
    private static partial Regex NicknamePattern();

    /// <summary>USR-02: perfil propio con estadísticas (caché 60 s por usuario).</summary>
    public async Task<ProfileResponse> GetMeAsync(ObjectId userId, CancellationToken cancellationToken)
    {
        var version = await _cache.GetVersionAsync(CacheExtensions.UserScope(userId), cancellationToken);
        return await _cache.GetOrCreateJsonAsync(
            $"users:me:{userId}:{version}",
            ProfileTtl,
            async () =>
            {
                var user = await _users.GetByIdAsync(userId, cancellationToken) ?? throw ApiException.Unauthorized();
                var groups = await _members.CountActiveGroupsAsync(userId, cancellationToken);
                return ToProfile(user, groups);
            },
            cancellationToken);
    }

    /// <summary>USR-03: cambia apodo y/o avatar; el apodo es único y pasa por moderación (falla cerrada).</summary>
    public async Task<ProfileResponse> UpdateMeAsync(ObjectId userId, UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        if (request.Nickname is null && request.AvatarId is null)
        {
            throw ApiException.Unprocessable(ErrorCodes.ValidationError, "Envía nickname o avatar_id.");
        }

        var user = await _users.GetByIdAsync(userId, cancellationToken) ?? throw ApiException.Unauthorized();
        var nickname = request.Nickname?.Trim();
        var nicknameChanged = nickname is not null && !string.Equals(nickname, user.Nickname, StringComparison.Ordinal);

        if (nicknameChanged)
        {
            var existing = await _users.GetByNicknameAsync(nickname!.ToLowerInvariant(), cancellationToken);
            if (existing is not null && existing.Id != userId)
            {
                throw ApiException.Conflict(ErrorCodes.NicknameAlreadyExists, "El apodo ya está en uso.");
            }

            var moderation = await _moderation.ModerateTextAsync(nickname, cancellationToken);
            if (!moderation.Approved)
            {
                throw ApiException.Unprocessable(ErrorCodes.ContentRejected, "El apodo fue rechazado por moderación.", new { labels = moderation.Labels });
            }
        }

        var updated = await _users.UpdateProfileAsync(
            userId,
            nicknameChanged ? nickname : null,
            request.AvatarId,
            DateTime.UtcNow,
            cancellationToken) ?? throw ApiException.Unauthorized();

        if (nicknameChanged)
        {
            await _members.UpdateNickLowerAsync(userId, updated.NicknameLower, cancellationToken);
            foreach (var groupId in await _members.ListActiveGroupIdsAsync(userId, cancellationToken))
            {
                await _cache.BumpVersionAsync(CacheExtensions.GroupScope(groupId), cancellationToken);
            }
        }

        await _cache.BumpVersionAsync(CacheExtensions.UserScope(userId), cancellationToken);
        await _audit.AppendAsync(
            "user.profile_updated",
            userId,
            "user",
            userId,
            null,
            new { nickname_changed = nicknameChanged, avatar_changed = request.AvatarId is not null },
            cancellationToken);

        var groups = await _members.CountActiveGroupsAsync(userId, cancellationToken);
        return ToProfile(updated, groups);
    }

    /// <summary>
    /// USR-05: perfil público de alguien con quien se comparte al menos un grupo (CLARIFICACION #14):
    /// apodo, avatar, estadísticas e Índice de Palabra. Nunca el correo.
    /// </summary>
    public async Task<PublicProfileResponse> GetPublicAsync(ObjectId viewerId, string id, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(id, out var targetId))
        {
            throw ApiException.NotFound("Usuario no encontrado.");
        }

        var target = await _users.GetByIdAsync(targetId, cancellationToken);
        if (target is null || target.Status != "active")
        {
            throw ApiException.NotFound("Usuario no encontrado.");
        }

        if (targetId != viewerId)
        {
            var mine = await _members.ListActiveGroupIdsAsync(viewerId, cancellationToken);
            var theirs = await _members.ListActiveGroupIdsAsync(targetId, cancellationToken);
            if (!mine.Intersect(theirs).Any())
            {
                throw ApiException.Forbidden(ErrorCodes.NoSharedGroup, "Solo puedes ver perfiles de personas de tus grupos.");
            }
        }

        var version = await _cache.GetVersionAsync(CacheExtensions.UserScope(targetId), cancellationToken);
        return await _cache.GetOrCreateJsonAsync(
            $"users:public:{targetId}:{version}",
            ProfileTtl,
            () => Task.FromResult(new PublicProfileResponse
            {
                Id = target.Id.ToString(),
                Nickname = target.Nickname,
                AvatarId = target.AvatarId,
                Stats = ToStats(target.Stats),
                CreatedAt = target.CreatedAt
            }),
            cancellationToken);
    }

    /// <summary>DEV-03: activa o desactiva cada tipo de notificación push.</summary>
    public async Task<NotificationPreferencesDto> UpdatePreferencesAsync(ObjectId userId, NotificationPreferencesDto request, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken) ?? throw ApiException.Unauthorized();
        var preferences = user.NotificationPreferences;
        preferences.Retos = request.Retos ?? preferences.Retos;
        preferences.Votos = request.Votos ?? preferences.Votos;
        preferences.Torneos = request.Torneos ?? preferences.Torneos;
        preferences.Compromisos = request.Compromisos ?? preferences.Compromisos;
        var updated = await _users.UpdateNotificationPreferencesAsync(userId, preferences, DateTime.UtcNow, cancellationToken)
            ?? throw ApiException.Unauthorized();
        await _cache.BumpVersionAsync(CacheExtensions.UserScope(userId), cancellationToken);
        return NotificationPreferencesDto.From(updated.NotificationPreferences);
    }

    /// <summary>USR-06: endpoint público; no revela nada más que si el apodo está libre.</summary>
    public async Task<NicknameAvailabilityResponse> CheckNicknameAsync(string? nickname, CancellationToken cancellationToken)
    {
        var value = nickname?.Trim() ?? string.Empty;
        if (!NicknamePattern().IsMatch(value))
        {
            throw ApiException.Unprocessable(
                ErrorCodes.ValidationError,
                "El apodo debe tener 3 a 24 caracteres: letras, números, punto, guion o guion bajo.");
        }

        var existing = await _users.GetByNicknameAsync(value.ToLowerInvariant(), cancellationToken);
        return new NicknameAvailabilityResponse { Nickname = value, Available = existing is null };
    }

    private static ProfileResponse ToProfile(User user, int groupsCount) => new()
    {
        Id = user.Id.ToString(),
        Email = user.Email,
        Nickname = user.Nickname,
        AvatarId = user.AvatarId,
        EmailVerified = user.EmailVerified,
        Stats = ToStats(user.Stats),
        GroupsCount = groupsCount,
        NotificationPreferences = NotificationPreferencesDto.From(user.NotificationPreferences),
        CreatedAt = user.CreatedAt,
        UpdatedAt = user.UpdatedAt == default ? user.CreatedAt : user.UpdatedAt
    };

    private static UserStatsResponse ToStats(UserStats stats) => new()
    {
        Completed = stats.Completed,
        Cheated = stats.Cheated,
        Streak = stats.Streak,
        Trophies = stats.Trophies,
        WordIndex = stats.WordIndex
    };
}
