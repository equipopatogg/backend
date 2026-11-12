using MongoDB.Bson;
using MongoDB.Driver;
using Pactogg.Api.Modules.Tournaments.Abstractions;
using Pactogg.Api.Modules.Tournaments.Domain;

namespace Pactogg.Api.Infrastructure.Mongo.Repositories;

public sealed class MongoMatchRepository : IMatchRepository
{
    private readonly MongoContext _context;

    public MongoMatchRepository(MongoContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Match>> ListForTournamentIdsAsync(
        IReadOnlyCollection<ObjectId> tournamentIds,
        CancellationToken cancellationToken)
    {
        if (tournamentIds.Count == 0)
        {
            return [];
        }

        return await _context.Matches.Find(x => tournamentIds.Contains(x.TournamentId))
            .SortBy(x => x.Round)
            .ThenBy(x => x.Position)
            .ToListAsync(cancellationToken);
    }

    public async Task<Match?> GetByIdAsync(ObjectId id, CancellationToken cancellationToken) =>
        await _context.Matches.Find(x => x.Id == id).FirstOrDefaultAsync(cancellationToken);

    public async Task<Match?> FindNextForGroupsAsync(IReadOnlyCollection<ObjectId> groupIds, CancellationToken cancellationToken)
    {
        if (groupIds.Count == 0)
        {
            return null;
        }

        var builder = Builders<Match>.Filter;
        var filter = builder.Eq(x => x.Status, "pending")
            & builder.Ne(x => x.P1GroupId, null)
            & builder.Ne(x => x.P2GroupId, null)
            & (builder.In(x => x.P1GroupId, groupIds.Select(x => (ObjectId?)x)) | builder.In(x => x.P2GroupId, groupIds.Select(x => (ObjectId?)x)));
        return await _context.Matches.Find(filter).SortBy(x => x.Round).ThenBy(x => x.Id).FirstOrDefaultAsync(cancellationToken);
    }
}
