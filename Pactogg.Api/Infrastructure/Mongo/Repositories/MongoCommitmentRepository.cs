using MongoDB.Bson;
using MongoDB.Driver;
using Pactogg.Api.Modules.Commitments.Abstractions;
using Pactogg.Api.Modules.Commitments.Domain;

namespace Pactogg.Api.Infrastructure.Mongo.Repositories;

public sealed class MongoCommitmentRepository : ICommitmentRepository
{
    private readonly MongoContext _context;

    public MongoCommitmentRepository(MongoContext context)
    {
        _context = context;
    }

    public async Task<Commitment?> GetByIdAsync(ObjectId id, CancellationToken cancellationToken) =>
        await _context.Commitments.Find(x => x.Id == id).FirstOrDefaultAsync(cancellationToken);

    public async Task<(IReadOnlyList<Commitment> Items, bool HasMore)> ListAsync(
        ObjectId groupId,
        ObjectId userId,
        bool asDebtor,
        string? status,
        int limit,
        ObjectId? cursor,
        CancellationToken cancellationToken)
    {
        var builder = Builders<Commitment>.Filter;
        var filter = builder.Eq(x => x.GroupId, groupId)
            & (asDebtor ? builder.Eq(x => x.DebtorId, userId) : builder.Eq(x => x.CreditorId, userId));
        if (!string.IsNullOrWhiteSpace(status))
        {
            filter &= builder.Eq(x => x.Status, status);
        }

        if (cursor.HasValue)
        {
            filter &= builder.Lt(x => x.Id, cursor.Value);
        }

        var items = await _context.Commitments.Find(filter).SortByDescending(x => x.Id).Limit(limit + 1).ToListAsync(cancellationToken);
        return (items.Take(limit).ToList(), items.Count > limit);
    }

    public async Task<Commitment?> TryTransitionAsync(
        ObjectId id,
        IReadOnlyCollection<string> fromStatuses,
        string toStatus,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var update = Builders<Commitment>.Update.Set(x => x.Status, toStatus).Set(x => x.UpdatedAt, now);
        update = toStatus switch
        {
            "completed" => update.Set(x => x.CompletedAt, now),
            "completion_requested" => update.Set(x => x.RequestedAt, now),
            _ => update
        };

        return await _context.Commitments.FindOneAndUpdateAsync(
            Builders<Commitment>.Filter.Eq(x => x.Id, id) & Builders<Commitment>.Filter.In(x => x.Status, fromStatuses),
            update,
            new FindOneAndUpdateOptions<Commitment> { ReturnDocument = ReturnDocument.After },
            cancellationToken);
    }
}
