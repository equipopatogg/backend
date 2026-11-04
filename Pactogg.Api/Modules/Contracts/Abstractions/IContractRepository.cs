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
    Task MarkVotingAsync(ObjectId id, CancellationToken cancellationToken);

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
