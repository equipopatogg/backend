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
        (int)await _context.Evidences.CountDocumentsAsync(x => x.ContractId == contractId && x.Moderation.Status == "approved", cancellationToken: cancellationToken);

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

    public async Task<EvidenceEntity?> GetByIdAsync(ObjectId id, CancellationToken cancellationToken) =>
        await _context.Evidences.Find(x => x.Id == id).FirstOrDefaultAsync(cancellationToken);

    public async Task<EvidenceEntity?> MarkRemovedAsync(ObjectId id, DateTime now, CancellationToken cancellationToken) =>
        await _context.Evidences.FindOneAndUpdateAsync(
            Builders<EvidenceEntity>.Filter.Where(x => x.Id == id && x.Moderation.Status != "removed"),
            Builders<EvidenceEntity>.Update.Set(x => x.Moderation.Status, "removed").Set(x => x.Moderation.ModeratedAt, now).Set(x => x.SavedInFame, false),
            new FindOneAndUpdateOptions<EvidenceEntity> { ReturnDocument = ReturnDocument.Before },
            cancellationToken);

    public async Task<EvidenceEntity?> ReplaceContentAsync(ObjectId id, string type, string? url, string? provider, string? objectKey, long? sizeBytes, DateTime? expiresAt, DateTime now, CancellationToken cancellationToken) =>
        await _context.Evidences.FindOneAndUpdateAsync(
            Builders<EvidenceEntity>.Filter.Where(x => x.Id == id && x.Moderation.Status == "approved"),
            Builders<EvidenceEntity>.Update
                .Set(x => x.Type, type)
                .Set(x => x.Url, url)
                .Set(x => x.Provider, provider)
                .Set(x => x.ObjectKey, objectKey)
                .Set(x => x.SizeBytes, sizeBytes)
                .Set(x => x.ExpiresAt, expiresAt)
                .Set(x => x.Moderation.ModeratedAt, now)
                .Set(x => x.CreatedAt, now),
            new FindOneAndUpdateOptions<EvidenceEntity> { ReturnDocument = ReturnDocument.Before },
            cancellationToken);

    public async Task<EvidenceEntity?> DeleteAsync(ObjectId id, CancellationToken cancellationToken) =>
        await _context.Evidences.FindOneAndDeleteAsync(x => x.Id == id && x.Moderation.Status == "approved", cancellationToken: cancellationToken);

    public async Task<EvidenceEntity?> SaveInFameAsync(ObjectId id, string objectKey, CancellationToken cancellationToken) =>
        await _context.Evidences.FindOneAndUpdateAsync(
            Builders<EvidenceEntity>.Filter.Where(x => x.Id == id && x.Moderation.Status == "approved"),
            Builders<EvidenceEntity>.Update.Set(x => x.SavedInFame, true).Set(x => x.ObjectKey, objectKey).Set(x => x.ExpiresAt, null),
            new FindOneAndUpdateOptions<EvidenceEntity> { ReturnDocument = ReturnDocument.After },
            cancellationToken);

    public async Task<IReadOnlyList<EvidenceEntity>> ListExpiredPhotosAsync(DateTime now, int limit, CancellationToken cancellationToken) =>
        await _context.Evidences
            .Find(x => x.Type == "photo" && x.Moderation.Status == "approved" && !x.SavedInFame && x.ExpiresAt != null && x.ExpiresAt <= now)
            .Limit(limit)
            .ToListAsync(cancellationToken);

    public Task MarkExpiredAsync(ObjectId id, DateTime now, CancellationToken cancellationToken) =>
        _context.Evidences.UpdateOneAsync(
            x => x.Id == id && x.Moderation.Status == "approved" && !x.SavedInFame,
            Builders<EvidenceEntity>.Update.Set(x => x.Moderation.Status, "expired").Set(x => x.Moderation.ModeratedAt, now),
            cancellationToken: cancellationToken);

    public async Task<IReadOnlyList<EvidenceEntity>> ListPhotosByUserAsync(ObjectId userId, CancellationToken cancellationToken) =>
        await _context.Evidences.Find(x => x.UserId == userId && x.Type == "photo" && x.Moderation.Status != "removed").ToListAsync(cancellationToken);
}
