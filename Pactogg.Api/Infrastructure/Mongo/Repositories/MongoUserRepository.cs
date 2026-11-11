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

    public async Task<User?> GetByGoogleSubAsync(string googleSub, CancellationToken cancellationToken) =>
        await _context.Users.Find(x => x.GoogleSub == googleSub && x.DeletedAt == null)
            .FirstOrDefaultAsync(cancellationToken);

    public Task LinkGoogleAsync(ObjectId id, string googleSub, DateTime at, CancellationToken cancellationToken) =>
        _context.Users.UpdateOneAsync(
            x => x.Id == id,
            Builders<User>.Update.Set(x => x.GoogleSub, googleSub).Set(x => x.EmailVerified, true).Set(x => x.UpdatedAt, at),
            cancellationToken: cancellationToken);

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

    public Task SetEmailVerifiedAsync(ObjectId id, DateTime at, CancellationToken cancellationToken) =>
        _context.Users.UpdateOneAsync(
            x => x.Id == id,
            Builders<User>.Update.Set(x => x.EmailVerified, true).Set(x => x.UpdatedAt, at),
            cancellationToken: cancellationToken);

    public Task UpdatePasswordAsync(ObjectId id, string passwordHash, DateTime at, CancellationToken cancellationToken) =>
        _context.Users.UpdateOneAsync(
            x => x.Id == id,
            Builders<User>.Update
                .Set(x => x.PasswordHash, passwordHash)
                .Set(x => x.PasswordChangedAt, at)
                .Set(x => x.UpdatedAt, at),
            cancellationToken: cancellationToken);

    public Task UpdateConsentAsync(ObjectId id, string version, DateTime at, CancellationToken cancellationToken) =>
        _context.Users.UpdateOneAsync(
            x => x.Id == id,
            Builders<User>.Update
                .Set(x => x.Consent, new ConsentRecord { Version = version, AcceptedAt = at })
                .Set(x => x.UpdatedAt, at),
            cancellationToken: cancellationToken);

    public async Task<User?> UpdateNotificationPreferencesAsync(ObjectId id, NotificationPreferences preferences, DateTime at, CancellationToken cancellationToken) =>
        await _context.Users.FindOneAndUpdateAsync(
            Builders<User>.Filter.Where(x => x.Id == id && x.DeletedAt == null),
            Builders<User>.Update.Set(x => x.NotificationPreferences, preferences).Set(x => x.UpdatedAt, at),
            new FindOneAndUpdateOptions<User> { ReturnDocument = ReturnDocument.After },
            cancellationToken);

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

    public async Task<UserStats?> ApplyStatsAsync(ObjectId id, StatsDelta delta, DateTime now, CancellationToken cancellationToken)
    {
        // Los contadores se suman de forma atómica; el índice se escribe solo si los contadores siguen iguales
        // a los que se usaron para calcularlo, así una escritura vieja nunca pisa a una más nueva.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var update = Builders<User>.Update.Set(x => x.UpdatedAt, now);
            if (attempt == 0)
            {
                update = update
                    .Inc(x => x.Stats.Expired, delta.Expired)
                    .Inc(x => x.Stats.CommitmentsOnTime, delta.CommitmentsOnTime)
                    .Inc(x => x.Stats.CommitmentsOverdue, delta.CommitmentsOverdue)
                    .Inc(x => x.Stats.AlignedVotes, delta.AlignedVotes);
            }

            var user = await _context.Users.FindOneAndUpdateAsync(
                x => x.Id == id,
                update,
                new FindOneAndUpdateOptions<User> { ReturnDocument = ReturnDocument.After },
                cancellationToken);
            if (user is null)
            {
                return null;
            }

            var s = user.Stats;
            var index = Modules.Reputation.WordIndexCalculator.Compute(s);
            var result = await _context.Users.UpdateOneAsync(
                x => x.Id == id
                    && x.Stats.Completed == s.Completed
                    && x.Stats.Cheated == s.Cheated
                    && x.Stats.Expired == s.Expired
                    && x.Stats.CommitmentsOnTime == s.CommitmentsOnTime
                    && x.Stats.CommitmentsOverdue == s.CommitmentsOverdue
                    && x.Stats.AlignedVotes == s.AlignedVotes,
                Builders<User>.Update.Set(x => x.Stats.WordIndex, index),
                cancellationToken: cancellationToken);
            if (result.MatchedCount > 0)
            {
                s.WordIndex = index;
                return s;
            }
        }

        return (await GetByIdAsync(id, cancellationToken))?.Stats;
    }

    public Task AnonymizeAsync(ObjectId id, DateTime now, CancellationToken cancellationToken) =>
        _context.Users.UpdateOneAsync(
            x => x.Id == id,
            Builders<User>.Update
                .Set(x => x.Email, $"eliminado-{id}@pactogg.invalid")
                .Set(x => x.PasswordHash, string.Empty)
                .Set(x => x.GoogleSub, null)
                .Set(x => x.Nickname, "Usuario eliminado")
                .Set(x => x.NicknameLower, $"eliminado:{id}")
                .Set(x => x.AvatarId, "default")
                .Set(x => x.EmailVerified, false)
                .Set(x => x.Consent, new ConsentRecord { Version = string.Empty, AcceptedAt = DateTime.MinValue })
                .Set(x => x.Status, "deleted")
                .Set(x => x.DeletedAt, now)
                .Set(x => x.UpdatedAt, now),
            cancellationToken: cancellationToken);
}
