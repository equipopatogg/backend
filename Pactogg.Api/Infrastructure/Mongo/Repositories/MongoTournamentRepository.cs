using MongoDB.Bson;
using MongoDB.Driver;
using Pactogg.Api.Modules.Tournaments.Abstractions;
using Pactogg.Api.Modules.Tournaments.Domain;

namespace Pactogg.Api.Infrastructure.Mongo.Repositories;

public sealed class MongoTournamentRepository : ITournamentRepository
{
    private readonly MongoContext _context;

    public MongoTournamentRepository(MongoContext context)
    {
        _context = context;
    }

    public async Task<(IReadOnlyList<Tournament> Items, bool HasMore)> ListForGroupsAsync(
        IReadOnlyCollection<ObjectId> groupIds,
        string? status,
        string? game,
        ObjectId? groupId,
        int limit,
        ObjectId? cursor,
        CancellationToken cancellationToken)
    {
        if (groupIds.Count == 0)
        {
            return ([], false);
        }

        var filter = Builders<Tournament>.Filter.In(x => x.GroupId, groupIds);
        if (!string.IsNullOrWhiteSpace(status))
        {
            filter &= Builders<Tournament>.Filter.Eq(x => x.Status, status);
        }

        if (!string.IsNullOrWhiteSpace(game))
        {
            filter &= Builders<Tournament>.Filter.Eq(x => x.Game, game);
        }

        if (groupId.HasValue)
        {
            filter &= Builders<Tournament>.Filter.Eq(x => x.GroupId, groupId.Value);
        }

        if (cursor.HasValue)
        {
            filter &= Builders<Tournament>.Filter.Lt(x => x.Id, cursor.Value);
        }

        var items = await _context.Tournaments.Find(filter)
            .SortByDescending(x => x.Id)
            .Limit(limit + 1)
            .ToListAsync(cancellationToken);

        var hasMore = items.Count > limit;
        return (items.Take(limit).ToList(), hasMore);
    }
}
