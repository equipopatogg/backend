using MongoDB.Bson;
using Pactogg.Api.Modules.Tournaments.Domain;

namespace Pactogg.Api.Modules.Tournaments.Abstractions;

public enum TournamentWriteOutcome
{
    Ok,
    InsufficientPoints,
    StateChanged
}

public sealed record TournamentWriteResult(TournamentWriteOutcome Outcome, Tournament? Tournament);

public interface ITournamentRepository
{
    /// <summary>TOR-02: torneos en los que participa (o organiza) alguno de los grupos.</summary>
    Task<(IReadOnlyList<Tournament> Items, bool HasMore)> ListForGroupsAsync(
        IReadOnlyCollection<ObjectId> groupIds,
        string? status,
        string? game,
        ObjectId? groupId,
        int limit,
        ObjectId? cursor,
        CancellationToken cancellationToken);

    Task<Tournament?> GetByIdAsync(ObjectId id, CancellationToken cancellationToken);

    Task<Tournament?> GetByCodeAsync(string code, CancellationToken cancellationToken);

    Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken);

    /// <summary>Un torneo open o in_progress organizado por el grupo (límite de uno a la vez).</summary>
    Task<bool> HasActiveForOrganizerGroupAsync(ObjectId groupId, CancellationToken cancellationToken);

    /// <summary>
    /// TOR-01 en una transacción: crea el torneo con el grupo organizador inscrito y cobra la inscripción
    /// a quien lo crea (tournament_entry). Sin saldo → InsufficientPoints y no se guarda nada.
    /// </summary>
    Task<TournamentWriteResult> CreateAsync(Tournament tournament, DateTime now, CancellationToken cancellationToken);

    /// <summary>TOR-06 en una transacción: agrega el grupo, suma a la bolsa y cobra a quien inscribe.</summary>
    Task<TournamentWriteResult> JoinAsync(ObjectId id, int expectedMaxGroups, int amount, ObjectId groupId, ObjectId payerId, DateTime now, CancellationToken cancellationToken);

    /// <summary>TOR-07 en una transacción: saca al grupo (no al organizador) y devuelve la inscripción.</summary>
    Task<Tournament?> LeaveAsync(ObjectId id, ObjectId groupId, DateTime now, CancellationToken cancellationToken);

    /// <summary>TOR-04: solo en open y sin bajar el tamaño por debajo de los inscritos.</summary>
    Task<Tournament?> UpdateAsync(ObjectId id, string game, DateTime scheduledAt, int maxGroups, DateTime now, CancellationToken cancellationToken);

    /// <summary>TOR-05 y 010-R16 en una transacción: cancela, devuelve todas las inscripciones activas y cancela las partidas abiertas.</summary>
    Task<Tournament?> CancelAsync(ObjectId id, string reason, DateTime now, CancellationToken cancellationToken);

    /// <summary>
    /// 010-R14 en una transacción: in_progress → finished con el grupo campeón y acredita cada premio (tournament_prize)
    /// a su representante en el grupo campeón. Null si ya no estaba en curso.
    /// </summary>
    Task<Tournament?> FinishAsync(ObjectId id, ObjectId winnerGroupId, IReadOnlyList<(ObjectId UserId, int Amount)> prizes, DateTime now, CancellationToken cancellationToken);

    /// <summary>010-R16: torneos open o in_progress en los que participa el grupo.</summary>
    Task<IReadOnlyList<Tournament>> ListActiveForGroupAsync(ObjectId groupId, CancellationToken cancellationToken);

    /// <summary>TOR-08 en una transacción: si los inscritos siguen siendo los mismos, pasa a in_progress y guarda el bracket.</summary>
    Task<Tournament?> StartAsync(ObjectId id, IReadOnlyList<ObjectId> expectedParticipants, IReadOnlyList<Match> matches, DateTime now, CancellationToken cancellationToken);
}
