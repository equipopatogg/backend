using MongoDB.Bson;
using Pactogg.Api.Modules.Tournaments.Domain;

namespace Pactogg.Api.Modules.Tournaments.Abstractions;

public interface IMatchRepository
{
    Task<IReadOnlyList<Match>> ListForTournamentIdsAsync(
        IReadOnlyCollection<ObjectId> tournamentIds,
        CancellationToken cancellationToken);
}
