using MongoDB.Bson;
using MongoDB.Driver;
using Pactogg.Api.Common;
using Pactogg.Api.Modules.Auth.Abstractions;
using Pactogg.Api.Modules.Auth.Domain;

namespace Pactogg.Api.Infrastructure.Mongo.Repositories;

public sealed class MongoUserRepository : IUserRepository
{
    private readonly MongoContext _context;

    public MongoUserRepository(MongoContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        await _context.Users.Find(x => x.Email == normalizedEmail && x.DeletedAt == null)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<User?> GetByIdAsync(ObjectId id, CancellationToken cancellationToken) =>
        await _context.Users.Find(x => x.Id == id && x.DeletedAt == null)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<User?> GetByNicknameAsync(string nicknameLower, CancellationToken cancellationToken) =>
        await _context.Users.Find(x => x.NicknameLower == nicknameLower && x.DeletedAt == null)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task CreateAsync(User user, CancellationToken cancellationToken)
    {
        try
        {
            await _context.Users.InsertOneAsync(user, cancellationToken: cancellationToken);
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            if (ex.WriteError.Message.Contains("nickname", StringComparison.OrdinalIgnoreCase))
            {
                throw ApiException.Conflict(ErrorCodes.NicknameAlreadyExists, "El apodo ya está en uso.");
            }

            throw ApiException.Conflict(ErrorCodes.EmailAlreadyExists, "Ya existe una cuenta con ese correo.");
        }
    }

    public Task UpdateLastLoginAsync(ObjectId id, DateTime at, CancellationToken cancellationToken) =>
        _context.Users.UpdateOneAsync(
            x => x.Id == id,
            Builders<User>.Update.Set(x => x.LastLoginAt, at).Set(x => x.UpdatedAt, at),
            cancellationToken: cancellationToken);
}
