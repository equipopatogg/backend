using MongoDB.Bson;
using EvidenceEntity = Pactogg.Api.Modules.Evidence.Domain.Evidence;

namespace Pactogg.Api.Modules.Evidence.Abstractions;

public interface IEvidenceRepository
{
    Task CreateAsync(EvidenceEntity evidence, CancellationToken cancellationToken);
    Task<int> CountByContractAsync(ObjectId contractId, CancellationToken cancellationToken);
    Task<IReadOnlyList<EvidenceEntity>> ListByContractIdsAsync(IReadOnlyCollection<ObjectId> contractIds, CancellationToken cancellationToken);

    Task<EvidenceEntity?> GetByIdAsync(ObjectId id, CancellationToken cancellationToken);

    /// <summary>REP-03/04: la evidencia deja de mostrarse (moderation.status = removed). Devuelve la evidencia antes del cambio.</summary>
    Task<EvidenceEntity?> MarkRemovedAsync(ObjectId id, DateTime now, CancellationToken cancellationToken);

    /// <summary>USR-04: fotos subidas por la persona que siguen visibles.</summary>
    Task<IReadOnlyList<EvidenceEntity>> ListPhotosByUserAsync(ObjectId userId, CancellationToken cancellationToken);
}
