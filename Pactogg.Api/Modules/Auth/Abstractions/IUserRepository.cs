using MongoDB.Bson;
using Pactogg.Api.Modules.Auth.Domain;

namespace Pactogg.Api.Modules.Auth.Abstractions;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);
    Task<User?> GetByIdAsync(ObjectId id, CancellationToken cancellationToken);
    Task<User?> GetByNicknameAsync(string nicknameLower, CancellationToken cancellationToken);
    Task CreateAsync(User user, CancellationToken cancellationToken);
    Task UpdateLastLoginAsync(ObjectId id, DateTime at, CancellationToken cancellationToken);
}
