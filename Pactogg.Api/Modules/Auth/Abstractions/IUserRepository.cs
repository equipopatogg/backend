using MongoDB.Bson;
using Pactogg.Api.Modules.Auth.Domain;

namespace Pactogg.Api.Modules.Auth.Abstractions;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);
    Task<User?> GetByIdAsync(ObjectId id, CancellationToken cancellationToken);
    Task<User?> GetByNicknameAsync(string nicknameLower, CancellationToken cancellationToken);
    Task<User?> GetByGoogleSubAsync(string googleSub, CancellationToken cancellationToken);
    Task LinkGoogleAsync(ObjectId id, string googleSub, DateTime at, CancellationToken cancellationToken);
    Task<IReadOnlyList<User>> ListByIdsAsync(IReadOnlyCollection<ObjectId> ids, CancellationToken cancellationToken);
    Task CreateAsync(User user, CancellationToken cancellationToken);
    Task UpdateLastLoginAsync(ObjectId id, DateTime at, CancellationToken cancellationToken);

    /// <summary>Cambia apodo y/o avatar. Lanza 409 NICKNAME_ALREADY_EXISTS si el índice único lo rechaza.</summary>
    Task SetEmailVerifiedAsync(ObjectId id, DateTime at, CancellationToken cancellationToken);
    Task UpdatePasswordAsync(ObjectId id, string passwordHash, DateTime at, CancellationToken cancellationToken);
    Task UpdateConsentAsync(ObjectId id, string version, DateTime at, CancellationToken cancellationToken);
    Task<User?> UpdateNotificationPreferencesAsync(ObjectId id, NotificationPreferences preferences, DateTime at, CancellationToken cancellationToken);

    Task<User?> UpdateProfileAsync(ObjectId id, string? nickname, string? avatarId, DateTime at, CancellationToken cancellationToken);

    /// <summary>
    /// Spec 008-R12: suma los contadores y recalcula stats.word_index con <see cref="Pactogg.Api.Modules.Reputation.WordIndexCalculator"/>.
    /// Con <see cref="StatsDelta.None"/> solo recalcula (después de cerrar un reto). Devuelve las estadísticas nuevas.
    /// </summary>
    Task<UserStats?> ApplyStatsAsync(ObjectId id, StatsDelta delta, DateTime now, CancellationToken cancellationToken);

    /// <summary>USR-04: borra datos personales y deja el apodo "Usuario eliminado" (CLARIFICACION #15, #16).</summary>
    Task AnonymizeAsync(ObjectId id, DateTime now, CancellationToken cancellationToken);
}
