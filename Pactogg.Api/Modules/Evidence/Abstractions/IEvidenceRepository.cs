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

    /// <summary>EVI-04: reemplaza tipo, enlace u objeto de una evidencia aprobada. Devuelve la versión anterior o null si ya no estaba aprobada.</summary>
    Task<EvidenceEntity?> ReplaceContentAsync(ObjectId id, string type, string? url, string? provider, string? objectKey, long? sizeBytes, DateTime? expiresAt, DateTime now, CancellationToken cancellationToken);

    /// <summary>EVI-05: borra la evidencia aprobada. Devuelve la evidencia borrada o null si ya no existía.</summary>
    Task<EvidenceEntity?> DeleteAsync(ObjectId id, CancellationToken cancellationToken);

    /// <summary>EVI-06: marca la foto en el Muro de la Fama con su nueva llave y sin fecha de borrado.</summary>
    Task<EvidenceEntity?> SaveInFameAsync(ObjectId id, string objectKey, CancellationToken cancellationToken);

    /// <summary>007-R10: fotos aprobadas fuera de la Fama cuya fecha de borrado ya pasó.</summary>
    Task<IReadOnlyList<EvidenceEntity>> ListExpiredPhotosAsync(DateTime now, int limit, CancellationToken cancellationToken);

    /// <summary>007-R10: la foto vencida deja de mostrarse (moderation.status = expired).</summary>
    Task MarkExpiredAsync(ObjectId id, DateTime now, CancellationToken cancellationToken);

    /// <summary>USR-04: fotos subidas por la persona que siguen visibles.</summary>
    Task<IReadOnlyList<EvidenceEntity>> ListPhotosByUserAsync(ObjectId userId, CancellationToken cancellationToken);
}
