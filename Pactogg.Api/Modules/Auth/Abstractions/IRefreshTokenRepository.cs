using MongoDB.Bson;
using Pactogg.Api.Modules.Auth.Domain;

namespace Pactogg.Api.Modules.Auth.Abstractions;

public interface IRefreshTokenRepository
{
    Task CreateAsync(RefreshToken token, CancellationToken cancellationToken);
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken);
    Task RevokeAsync(ObjectId id, DateTime at, CancellationToken cancellationToken);
    Task MarkRotatedAsync(ObjectId id, DateTime at, CancellationToken cancellationToken);
    Task RevokeAllForUserAsync(ObjectId userId, DateTime at, CancellationToken cancellationToken);
}
