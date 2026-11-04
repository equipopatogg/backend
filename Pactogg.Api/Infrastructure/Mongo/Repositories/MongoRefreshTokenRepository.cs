using MongoDB.Bson;
using MongoDB.Driver;
using Pactogg.Api.Modules.Auth.Abstractions;
using Pactogg.Api.Modules.Auth.Domain;

namespace Pactogg.Api.Infrastructure.Mongo.Repositories;

public sealed class MongoRefreshTokenRepository : IRefreshTokenRepository
{
    private readonly MongoContext _context;

    public MongoRefreshTokenRepository(MongoContext context)
    {
        _context = context;
    }

    public Task CreateAsync(RefreshToken token, CancellationToken cancellationToken) =>
        _context.RefreshTokens.InsertOneAsync(token, cancellationToken: cancellationToken);

    public async Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        await _context.RefreshTokens.Find(x => x.TokenHash == tokenHash)
            .FirstOrDefaultAsync(cancellationToken);

    public Task RevokeAsync(ObjectId id, DateTime at, CancellationToken cancellationToken) =>
        _context.RefreshTokens.UpdateOneAsync(
            x => x.Id == id && x.RevokedAt == null,
            Builders<RefreshToken>.Update.Set(x => x.RevokedAt, at),
            cancellationToken: cancellationToken);

    public Task RevokeAllForUserAsync(ObjectId userId, DateTime at, CancellationToken cancellationToken) =>
        _context.RefreshTokens.UpdateManyAsync(
            x => x.UserId == userId && x.RevokedAt == null,
            Builders<RefreshToken>.Update.Set(x => x.RevokedAt, at),
            cancellationToken: cancellationToken);
}
