using MongoDB.Bson;
using Pactogg.Api.Modules.Tournaments.Domain;

namespace Pactogg.Api.Modules.Tournaments.Abstractions;

public interface IMatchRepository
{
    Task<IReadOnlyList<Match>> ListForTournamentIdsAsync(
        IReadOnlyCollection<ObjectId> tournamentIds,
        CancellationToken cancellationToken);

    Task<Match?> GetByIdAsync(ObjectId id, CancellationToken cancellationToken);

    /// <summary>MAT-07: partida pendiente con los dos grupos definidos en la que juega alguno de los grupos, de la ronda más baja.</summary>
    Task<Match?> FindNextForGroupsAsync(IReadOnlyCollection<ObjectId> groupIds, CancellationToken cancellationToken);
}
