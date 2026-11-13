using MongoDB.Bson;
using Pactogg.Api.Modules.Tournaments.Domain;

namespace Pactogg.Api.Modules.Tournaments.Abstractions;

public interface IMatchRepository
{
    Task<IReadOnlyList<Match>> ListForTournamentIdsAsync(
        IReadOnlyCollection<ObjectId> tournamentIds,
        CancellationToken cancellationToken);

    Task<Match?> GetByIdAsync(ObjectId id, CancellationToken cancellationToken);

    /// <summary>MAT-03: pending → reported con marcador, ganador, captura y representante. Null si ya cambió.</summary>
    Task<Match?> TryReportAsync(ObjectId id, int p1Score, int p2Score, ObjectId winnerGroupId, ObjectId reporterId, ObjectId reporterGroupId, bool reporterIsP1, string screenshotKey, DateTime now, CancellationToken cancellationToken);

    /// <summary>MAT-05: reported → disputed con motivo y representante rival.</summary>
    Task<Match?> TryDisputeAsync(ObjectId id, ObjectId userId, bool userIsP1, string reason, DateTime now, CancellationToken cancellationToken);

    /// <summary>
    /// MAT-04 y MAT-06: desde alguno de <paramref name="fromStatuses"/> → confirmed con ganador. Si <paramref name="clearScores"/>,
    /// borra el marcador (el organizador eligió otro ganador o declaró abandono). Null si ya cambió.
    /// </summary>
    Task<Match?> TryConfirmAsync(ObjectId id, IReadOnlyCollection<string> fromStatuses, ObjectId winnerGroupId, bool clearScores, ObjectId? confirmerId, bool confirmerIsP1, ObjectId? resolvedBy, string resolution, DateTime now, CancellationToken cancellationToken);

    /// <summary>Coloca al ganador en su lugar de la siguiente partida (posición par → p1).</summary>
    Task PlaceWinnerAsync(ObjectId nextMatchId, bool asP1, ObjectId groupId, DateTime now, CancellationToken cancellationToken);

    /// <summary>MAT-07: partida pendiente con los dos grupos definidos en la que juega alguno de los grupos, de la ronda más baja.</summary>
    Task<Match?> FindNextForGroupsAsync(IReadOnlyCollection<ObjectId> groupIds, CancellationToken cancellationToken);
}
