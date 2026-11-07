using MongoDB.Bson;
using MongoDB.Driver;
using Pactogg.Api.Modules.Reports;

namespace Pactogg.Api.Infrastructure.Mongo.Repositories;

public sealed class MongoReportRepository : IReportRepository
{
    private readonly MongoContext _context;

    public MongoReportRepository(MongoContext context)
    {
        _context = context;
    }

    public async Task<bool> TryCreateAsync(Report report, CancellationToken cancellationToken)
    {
        try
        {
            await _context.Reports.InsertOneAsync(report, cancellationToken: cancellationToken);
            return true;
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }

    public async Task<Report?> GetByIdAsync(ObjectId id, CancellationToken cancellationToken) =>
        await _context.Reports.Find(x => x.Id == id).FirstOrDefaultAsync(cancellationToken);

    public async Task<(IReadOnlyList<Report> Items, bool HasMore)> ListByGroupAsync(ObjectId groupId, string? status, int limit, ObjectId? cursor, CancellationToken cancellationToken)
    {
        var builder = Builders<Report>.Filter;
        var filter = builder.Eq(x => x.GroupId, groupId);
        if (status is not null) filter &= builder.Eq(x => x.Status, status);
        if (cursor is ObjectId c) filter &= builder.Lt(x => x.Id, c);
        var items = await _context.Reports.Find(filter).SortByDescending(x => x.Id).Limit(limit + 1).ToListAsync(cancellationToken);
        return (items.Take(limit).ToList(), items.Count > limit);
    }

    public async Task<Report?> TryResolveAsync(ObjectId id, string status, ObjectId by, DateTime now, CancellationToken cancellationToken) =>
        await _context.Reports.FindOneAndUpdateAsync(
            Builders<Report>.Filter.Where(x => x.Id == id && x.Status == "open"),
            Builders<Report>.Update.Set(x => x.Status, status).Set(x => x.ResolvedBy, by).Set(x => x.ResolvedAt, now),
            new FindOneAndUpdateOptions<Report> { ReturnDocument = ReturnDocument.After },
            cancellationToken);

    public Task ResolveSameTargetAsync(ObjectId groupId, string targetType, ObjectId targetId, string status, ObjectId by, DateTime now, CancellationToken cancellationToken) =>
        _context.Reports.UpdateManyAsync(
            x => x.GroupId == groupId && x.TargetType == targetType && x.TargetId == targetId && x.Status == "open",
            Builders<Report>.Update.Set(x => x.Status, status).Set(x => x.ResolvedBy, by).Set(x => x.ResolvedAt, now),
            cancellationToken: cancellationToken);
}

public sealed class MongoBlockRepository : IBlockRepository
{
    private readonly MongoContext _context;

    public MongoBlockRepository(MongoContext context)
    {
        _context = context;
    }

    public async Task<bool> TryBlockAsync(UserBlock block, CancellationToken cancellationToken)
    {
        try
        {
            await _context.Blocks.InsertOneAsync(block, cancellationToken: cancellationToken);
            return true;
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }

    public async Task<bool> UnblockAsync(ObjectId blockerId, ObjectId blockedId, CancellationToken cancellationToken) =>
        (await _context.Blocks.DeleteOneAsync(x => x.BlockerId == blockerId && x.BlockedId == blockedId, cancellationToken)).DeletedCount > 0;

    public async Task<bool> IsBlockedAsync(ObjectId blockerId, ObjectId blockedId, CancellationToken cancellationToken) =>
        await _context.Blocks.Find(x => x.BlockerId == blockerId && x.BlockedId == blockedId).AnyAsync(cancellationToken);

    public Task DeleteAllForUserAsync(ObjectId userId, CancellationToken cancellationToken) =>
        _context.Blocks.DeleteManyAsync(x => x.BlockerId == userId || x.BlockedId == userId, cancellationToken);
}
