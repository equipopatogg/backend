using MongoDB.Bson;
using Pactogg.Api.Modules.Contracts.Domain;

namespace Pactogg.Api.Modules.Contracts.Abstractions;

public interface IContractRepository
{
    Task CreateAsync(Contract contract, CancellationToken cancellationToken);
    Task<Contract?> GetByIdAsync(ObjectId id, CancellationToken cancellationToken);
    Task<Contract?> TryAttachEvidenceAsync(ObjectId id, ObjectId userId, DateTime now, int maxEvidence, CancellationToken cancellationToken);
    Task DecrementEvidenceAsync(ObjectId id, CancellationToken cancellationToken);
    Task MarkVotingAsync(ObjectId id, CancellationToken cancellationToken);
}
