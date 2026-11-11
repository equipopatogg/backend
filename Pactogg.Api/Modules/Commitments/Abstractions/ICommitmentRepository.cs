using MongoDB.Bson;
using Pactogg.Api.Modules.Commitments.Domain;

namespace Pactogg.Api.Modules.Commitments.Abstractions;

public interface ICommitmentRepository
{
    Task<Commitment?> GetByIdAsync(ObjectId id, CancellationToken cancellationToken);

    /// <summary>asDebtor=true → "debo"; false → "me deben".</summary>
    Task<(IReadOnlyList<Commitment> Items, bool HasMore)> ListAsync(
        ObjectId groupId,
        ObjectId userId,
        bool asDebtor,
        string? status,
        int limit,
        ObjectId? cursor,
        CancellationToken cancellationToken);

    /// <summary>Cambia el estado solo si el actual está en <paramref name="fromStatuses"/>. Null si ya cambió.</summary>
    Task<Commitment?> TryTransitionAsync(
        ObjectId id,
        IReadOnlyCollection<string> fromStatuses,
        string toStatus,
        DateTime now,
        CancellationToken cancellationToken);

    /// <summary>COM-05: compromisos abiertos (pending o completion_requested) como deudor o acreedor.</summary>
    Task<int> CountOpenAsync(ObjectId groupId, ObjectId userId, bool asDebtor, CancellationToken cancellationToken);

    /// <summary>Spec 009-R4: compromisos en pending creados antes de <paramref name="createdBefore"/> que todavía no se avisaron.</summary>
    Task<IReadOnlyList<Commitment>> ListOverdueCandidatesAsync(DateTime createdBefore, int limit, CancellationToken cancellationToken);

    /// <summary>Marca overdue_at una sola vez. Null si ya estaba marcado o dejó de estar pendiente.</summary>
    Task<Commitment?> TryMarkOverdueAsync(ObjectId id, DateTime now, CancellationToken cancellationToken);

    /// <summary>USR-04: cierra los compromisos abiertos de la persona (status cancelled).</summary>
    Task CloseOpenForUserAsync(ObjectId userId, DateTime now, CancellationToken cancellationToken);
}
