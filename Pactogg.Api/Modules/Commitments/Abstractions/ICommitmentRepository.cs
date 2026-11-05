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
}
