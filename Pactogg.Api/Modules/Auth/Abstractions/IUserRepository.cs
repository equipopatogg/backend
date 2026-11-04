using MongoDB.Bson;
using Pactogg.Api.Modules.Auth.Domain;

namespace Pactogg.Api.Modules.Auth.Abstractions;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);
    Task<User?> GetByIdAsync(ObjectId id, CancellationToken cancellationToken);
    Task<User?> GetByNicknameAsync(string nicknameLower, CancellationToken cancellationToken);
    Task<IReadOnlyList<User>> ListByIdsAsync(IReadOnlyCollection<ObjectId> ids, CancellationToken cancellationToken);
    Task CreateAsync(User user, CancellationToken cancellationToken);
    Task UpdateLastLoginAsync(ObjectId id, DateTime at, CancellationToken cancellationToken);

    /// <summary>Cambia apodo y/o avatar. Lanza 409 NICKNAME_ALREADY_EXISTS si el índice único lo rechaza.</summary>
    Task<User?> UpdateProfileAsync(ObjectId id, string? nickname, string? avatarId, DateTime at, CancellationToken cancellationToken);
}
