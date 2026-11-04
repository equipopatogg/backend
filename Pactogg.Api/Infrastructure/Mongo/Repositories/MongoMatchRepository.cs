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
}
