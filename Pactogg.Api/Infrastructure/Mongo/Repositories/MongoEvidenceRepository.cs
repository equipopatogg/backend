using MongoDB.Bson;
using MongoDB.Driver;
using Pactogg.Api.Modules.Evidence.Abstractions;
using EvidenceEntity = Pactogg.Api.Modules.Evidence.Domain.Evidence;

namespace Pactogg.Api.Infrastructure.Mongo.Repositories;

public sealed class MongoEvidenceRepository : IEvidenceRepository
{
    private readonly MongoContext _context;

    public MongoEvidenceRepository(MongoContext context)
    {
        _context = context;
    }

    public Task CreateAsync(EvidenceEntity evidence, CancellationToken cancellationToken) =>
        _context.Evidences.InsertOneAsync(evidence, cancellationToken: cancellationToken);

    public async Task<int> CountByContractAsync(ObjectId contractId, CancellationToken cancellationToken) =>
        (int)await _context.Evidences.CountDocumentsAsync(x => x.ContractId == contractId, cancellationToken: cancellationToken);

    public async Task<IReadOnlyList<EvidenceEntity>> ListByContractIdsAsync(
        IReadOnlyCollection<ObjectId> contractIds,
        CancellationToken cancellationToken)
    {
        if (contractIds.Count == 0)
        {
            return [];
        }

        return await _context.Evidences.Find(x => contractIds.Contains(x.ContractId))
            .SortBy(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}
