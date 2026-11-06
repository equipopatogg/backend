using MongoDB.Bson;
using Pactogg.Api.Modules.Contracts.Domain;

namespace Pactogg.Api.Modules.Contracts.Abstractions;

public interface IContractRepository
{
    Task CreateAsync(Contract contract, CancellationToken cancellationToken);
    Task<Contract?> GetByIdAsync(ObjectId id, CancellationToken cancellationToken);
    Task<(IReadOnlyList<Contract> Items, bool HasMore)> ListForGroupAsync(
        ObjectId groupId,
        ContractListFilter filter,
        int limit,
        ObjectId? cursor,
        CancellationToken cancellationToken);
    Task<Contract?> TryAttachEvidenceAsync(ObjectId id, ObjectId userId, DateTime now, int maxEvidence, CancellationToken cancellationToken);
    Task DecrementEvidenceAsync(ObjectId id, CancellationToken cancellationToken);
    /// <summary>Pasa a voting (si no lo estaba), fija quién ejecuta el reto, los jurados y el fin de la votación.</summary>
    Task MarkVotingAsync(ObjectId id, ObjectId executorId, IReadOnlyList<ObjectId> jurors, DateTime votingEndsAt, CancellationToken cancellationToken);

    /// <summary>VOT-02: registra un voto en el contador del contrato (solo jurados que no han votado, estado voting).</summary>
    Task<Contract?> TryRegisterVoteAsync(ObjectId id, ObjectId jurorId, bool legit, DateTime now, CancellationToken cancellationToken);

    /// <summary>Retos en voting donde el usuario es jurado y aún no vota (VOT-01).</summary>
    Task<(IReadOnlyList<Contract> Items, bool HasMore)> ListPendingVotesAsync(ObjectId jurorId, int limit, ObjectId? cursor, CancellationToken cancellationToken);

    /// <summary>Retos cumplidos del grupo, del más reciente al más antiguo (Muro de la Fama).</summary>
    Task<(IReadOnlyList<Contract> Items, bool HasMore)> ListFulfilledAsync(ObjectId groupId, int limit, ObjectId? cursor, CancellationToken cancellationToken);

    /// <summary>
    /// Cierra la votación en una transacción: voting → fulfilled|cheating, mueve la fianza
    /// (bond_release al ejecutor o bond_forfeit al retador), crea el compromiso si se cumplió y actualiza estadísticas.
    /// Null si el reto ya no estaba en voting.
    /// </summary>
    Task<Contract?> ResolveAsync(ObjectId id, string outcome, string resolvedBy, DateTime now, CancellationToken cancellationToken);

    /// <summary>CON-04: aplica cambios solo si sigue open y con la versión esperada.</summary>
    Task<Contract?> TryUpdateOpenAsync(ObjectId id, int expectedVersion, string game, string condition, string reward, DateTime deadline, int bond, DateTime now, CancellationToken cancellationToken);

    /// <summary>CON-05: open → cancelled por el retador (sin fianza retenida todavía).</summary>
    Task<Contract?> TryCancelOpenAsync(ObjectId id, int expectedVersion, DateTime now, CancellationToken cancellationToken);

    // ---- Ciclo de vida automático (ContractLifecycleService) ----

    /// <summary>Retos open/in_progress con fecha límite vencida.</summary>
    Task<IReadOnlyList<Contract>> ListPastDeadlineAsync(DateTime now, int limit, CancellationToken cancellationToken);

    /// <summary>Retos en voting cuyo plazo de jurado o de administrador ya venció, o que necesitan recordatorio.</summary>
    Task<IReadOnlyList<Contract>> ListVotingDueAsync(DateTime now, DateTime reminderThreshold, int limit, CancellationToken cancellationToken);

    /// <summary>open/in_progress → expired. Si había fianza retenida pasa al retador (bond_forfeit). Null si ya cambió.</summary>
    Task<Contract?> TryExpireAsync(ObjectId id, DateTime now, CancellationToken cancellationToken);

    /// <summary>Extiende la votación una sola vez.</summary>
    Task<Contract?> TryExtendVotingAsync(ObjectId id, DateTime newEnd, DateTime now, CancellationToken cancellationToken);

    /// <summary>Abre el plazo de decisión del administrador (si no estaba abierto).</summary>
    Task<Contract?> TryStartAdminPhaseAsync(ObjectId id, DateTime adminDeadline, DateTime now, CancellationToken cancellationToken);

    /// <summary>Agrega el jurado de desempate una sola vez.</summary>
    Task<Contract?> TryAddExtraJurorAsync(ObjectId id, ObjectId jurorId, DateTime now, CancellationToken cancellationToken);

    Task MarkReminderSentAsync(ObjectId id, CancellationToken cancellationToken);

    /// <summary>voting → cancelled (closed_reason) devolviendo la fianza a quien aceptó (bond_release). Null si ya cambió.</summary>
    Task<Contract?> TryCancelVotingWithRefundAsync(ObjectId id, string reason, DateTime now, CancellationToken cancellationToken);

    /// <summary>
    /// CON-06 en una sola transacción: open → in_progress (con versión esperada), descuenta la fianza
    /// del saldo del miembro solo si alcanza (nunca negativo) e inserta el movimiento bond_lock en points_ledger.
    /// </summary>
    Task<AcceptContractResult> AcceptWithBondAsync(
        ObjectId contractId,
        int expectedVersion,
        ObjectId userId,
        DateTime now,
        CancellationToken cancellationToken);

    /// <summary>CON-07: open → cancelled por el retado (con versión esperada). Null si el estado ya cambió.</summary>
    Task<Contract?> TryRejectAsync(ObjectId contractId, int expectedVersion, ObjectId userId, DateTime now, CancellationToken cancellationToken);
}

public sealed record ContractListFilter(string? Status, ObjectId? Mine, string? Game, string? Query);

public enum AcceptContractOutcome
{
    Accepted,
    StateChanged,
    InsufficientPoints
}

public sealed record AcceptContractResult(AcceptContractOutcome Outcome, Contract? Contract, int BalanceAfter);
