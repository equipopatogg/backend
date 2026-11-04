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

    public async Task<IReadOnlyList<User>> ListByIdsAsync(IReadOnlyCollection<ObjectId> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        return await _context.Users.Find(Builders<User>.Filter.In(x => x.Id, ids)).ToListAsync(cancellationToken);
    }

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

    public async Task<User?> UpdateProfileAsync(
        ObjectId id,
        string? nickname,
        string? avatarId,
        DateTime at,
        CancellationToken cancellationToken)
    {
        var update = Builders<User>.Update.Set(x => x.UpdatedAt, at);
        if (nickname is not null)
        {
            update = update
                .Set(x => x.Nickname, nickname)
                .Set(x => x.NicknameLower, nickname.ToLowerInvariant());
        }

        if (avatarId is not null)
        {
            update = update.Set(x => x.AvatarId, avatarId);
        }

        try
        {
            return await _context.Users.FindOneAndUpdateAsync(
                Builders<User>.Filter.Where(x => x.Id == id && x.DeletedAt == null),
                update,
                new FindOneAndUpdateOptions<User> { ReturnDocument = ReturnDocument.After },
                cancellationToken);
        }
        catch (MongoCommandException ex) when (ex.Code == 11000)
        {
            throw ApiException.Conflict(ErrorCodes.NicknameAlreadyExists, "El apodo ya está en uso.");
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            throw ApiException.Conflict(ErrorCodes.NicknameAlreadyExists, "El apodo ya está en uso.");
        }
    }
}
