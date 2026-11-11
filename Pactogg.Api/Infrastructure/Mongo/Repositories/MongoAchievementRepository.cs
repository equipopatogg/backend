using MongoDB.Bson;
using MongoDB.Driver;
using Pactogg.Api.Modules.Reputation;

namespace Pactogg.Api.Infrastructure.Mongo.Repositories;

public sealed class MongoAchievementRepository : IAchievementRepository
{
    private readonly MongoContext _context;

    public MongoAchievementRepository(MongoContext context)
    {
        _context = context;
    }

    public async Task<bool> TryAwardAsync(Achievement achievement, CancellationToken cancellationToken)
    {
        try
        {
            await _context.Achievements.InsertOneAsync(achievement, cancellationToken: cancellationToken);
            return true;
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<Achievement>> ListByUserAsync(ObjectId userId, CancellationToken cancellationToken) =>
        await _context.Achievements.Find(x => x.UserId == userId).SortByDescending(x => x.EarnedAt).Limit(200).ToListAsync(cancellationToken);
}
