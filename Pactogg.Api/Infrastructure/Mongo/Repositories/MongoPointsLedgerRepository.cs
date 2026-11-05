using MongoDB.Bson;
using MongoDB.Driver;
using Pactogg.Api.Modules.Points.Abstractions;
using Pactogg.Api.Modules.Points.Domain;

namespace Pactogg.Api.Infrastructure.Mongo.Repositories;

public sealed class MongoPointsLedgerRepository : IPointsLedgerRepository
{
    private readonly MongoContext _context;

    public MongoPointsLedgerRepository(MongoContext context)
    {
        _context = context;
    }

    public async Task<(IReadOnlyList<PointsLedgerEntry> Items, bool HasMore)> ListAsync(
        ObjectId groupId,
        ObjectId userId,
        string? type,
        int limit,
        ObjectId? cursor,
        CancellationToken cancellationToken)
    {
        var builder = Builders<PointsLedgerEntry>.Filter;
        var filter = builder.Eq(x => x.GroupId, groupId) & builder.Eq(x => x.UserId, userId);
        if (!string.IsNullOrWhiteSpace(type))
        {
            filter &= builder.Eq(x => x.Type, type);
        }

        if (cursor.HasValue)
        {
            filter &= builder.Lt(x => x.Id, cursor.Value);
        }

        var items = await _context.PointsLedger.Find(filter).SortByDescending(x => x.Id).Limit(limit + 1).ToListAsync(cancellationToken);
        return (items.Take(limit).ToList(), items.Count > limit);
    }
}
