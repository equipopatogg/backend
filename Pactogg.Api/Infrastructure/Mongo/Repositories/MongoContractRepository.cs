using MongoDB.Bson;
using MongoDB.Driver;
using Pactogg.Api.Modules.Contracts.Abstractions;
using Pactogg.Api.Modules.Contracts.Domain;

namespace Pactogg.Api.Infrastructure.Mongo.Repositories;

public sealed class MongoContractRepository : IContractRepository
{
    private readonly MongoContext _context;

    public MongoContractRepository(MongoContext context)
    {
        _context = context;
    }

    public Task CreateAsync(Contract contract, CancellationToken cancellationToken) =>
        _context.Contracts.InsertOneAsync(contract, cancellationToken: cancellationToken);

    public async Task<Contract?> GetByIdAsync(ObjectId id, CancellationToken cancellationToken) =>
        await _context.Contracts.Find(x => x.Id == id).FirstOrDefaultAsync(cancellationToken);

    public async Task<Contract?> TryAttachEvidenceAsync(
        ObjectId id,
        ObjectId userId,
        DateTime now,
        int maxEvidence,
        CancellationToken cancellationToken)
    {
        var filter = Builders<Contract>.Filter.And(
            Builders<Contract>.Filter.Eq(x => x.Id, id),
            Builders<Contract>.Filter.Lt(x => x.EvidenceCount, maxEvidence),
            Builders<Contract>.Filter.In(x => x.Status, ["open", "accepted", "in_progress", "evidence_pending", "voting"]));

        var update = Builders<Contract>.Update
            .Inc(x => x.EvidenceCount, 1)
            .Set(x => x.UpdatedAt, now);

        var options = new FindOneAndUpdateOptions<Contract>
        {
            ReturnDocument = ReturnDocument.After
        };

        return await _context.Contracts.FindOneAndUpdateAsync(filter, update, options, cancellationToken);
    }

    public Task DecrementEvidenceAsync(ObjectId id, CancellationToken cancellationToken) =>
        _context.Contracts.UpdateOneAsync(
            x => x.Id == id && x.EvidenceCount > 0,
            Builders<Contract>.Update.Inc(x => x.EvidenceCount, -1),
            cancellationToken: cancellationToken);

    public Task MarkVotingAsync(ObjectId id, CancellationToken cancellationToken) =>
        _context.Contracts.UpdateOneAsync(
            x => x.Id == id && x.Status != "voting",
            Builders<Contract>.Update
                .Set(x => x.Status, "voting")
                .Set(x => x.UpdatedAt, DateTime.UtcNow),
            cancellationToken: cancellationToken);
}
