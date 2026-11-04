using MongoDB.Bson;
using EvidenceEntity = Pactogg.Api.Modules.Evidence.Domain.Evidence;

namespace Pactogg.Api.Modules.Evidence.Abstractions;

public interface IEvidenceRepository
{
    Task CreateAsync(EvidenceEntity evidence, CancellationToken cancellationToken);
    Task<int> CountByContractAsync(ObjectId contractId, CancellationToken cancellationToken);
    Task<IReadOnlyList<EvidenceEntity>> ListByContractIdsAsync(IReadOnlyCollection<ObjectId> contractIds, CancellationToken cancellationToken);
}
