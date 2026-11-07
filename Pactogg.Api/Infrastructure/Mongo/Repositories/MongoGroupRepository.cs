using Regex = System.Text.RegularExpressions.Regex;
using MongoDB.Bson;
using MongoDB.Driver;
using Pactogg.Api.Modules.Groups.Abstractions;
using Pactogg.Api.Modules.Groups.Domain;

namespace Pactogg.Api.Infrastructure.Mongo.Repositories;

public sealed class MongoGroupRepository : IGroupRepository
{
    private readonly MongoContext _context;

    public MongoGroupRepository(MongoContext context)
    {
        _context = context;
    }

    public async Task<Group?> GetByIdAsync(ObjectId id, CancellationToken cancellationToken) =>
        await _context.Groups.Find(x => x.Id == id && x.DeletedAt == null)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<Group?> GetByInviteCodeAsync(string inviteCode, CancellationToken cancellationToken) =>
        await _context.Groups.Find(x => x.InviteCode == inviteCode && x.InviteActive && x.DeletedAt == null)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<(IReadOnlyList<Group> Items, bool HasMore)> ListByIdsAsync(
        IReadOnlyCollection<ObjectId> ids,
        string? nameContains,
        int limit,
        ObjectId? cursor,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return ([], false);
        }

        var builder = Builders<Group>.Filter;
        var filter = builder.In(x => x.Id, ids) & builder.Eq(x => x.DeletedAt, null);
        if (!string.IsNullOrWhiteSpace(nameContains))
        {
            filter &= builder.Regex(x => x.Name, new BsonRegularExpression(Regex.Escape(nameContains), "i"));
        }

        if (cursor.HasValue)
        {
            filter &= builder.Lt(x => x.Id, cursor.Value);
        }

        var items = await _context.Groups.Find(filter)
            .SortByDescending(x => x.Id)
            .Limit(limit + 1)
            .ToListAsync(cancellationToken);

        return (items.Take(limit).ToList(), items.Count > limit);
    }

    public Task CreateAsync(Group group, CancellationToken cancellationToken) =>
        _context.Groups.InsertOneAsync(group, cancellationToken: cancellationToken);

    public Task<bool> InviteCodeExistsAsync(string inviteCode, CancellationToken cancellationToken) =>
        _context.Groups.Find(x => x.InviteCode == inviteCode).AnyAsync(cancellationToken);

    public async Task<Group?> TryIncrementMemberCountAsync(ObjectId id, int maxMembers, DateTime at, CancellationToken cancellationToken) =>
        await _context.Groups.FindOneAndUpdateAsync(
            Builders<Group>.Filter.Where(x => x.Id == id && x.DeletedAt == null && x.MemberCount < maxMembers),
            Builders<Group>.Update.Inc(x => x.MemberCount, 1).Set(x => x.UpdatedAt, at),
            new FindOneAndUpdateOptions<Group> { ReturnDocument = ReturnDocument.After },
            cancellationToken);

    public Task DecrementMemberCountAsync(ObjectId id, CancellationToken cancellationToken) =>
        _context.Groups.UpdateOneAsync(
            x => x.Id == id && x.MemberCount > 0,
            Builders<Group>.Update.Inc(x => x.MemberCount, -1),
            cancellationToken: cancellationToken);

    public Task DeleteAsync(ObjectId id, CancellationToken cancellationToken) =>
        _context.Groups.DeleteOneAsync(x => x.Id == id, cancellationToken);

    public async Task<Group?> UpdateAsync(ObjectId id, string name, GroupSettings settings, DateTime now, CancellationToken cancellationToken) =>
        await _context.Groups.FindOneAndUpdateAsync(
            Builders<Group>.Filter.Where(x => x.Id == id && x.DeletedAt == null),
            Builders<Group>.Update.Set(x => x.Name, name).Set(x => x.Settings, settings).Set(x => x.UpdatedAt, now),
            new FindOneAndUpdateOptions<Group> { ReturnDocument = ReturnDocument.After },
            cancellationToken);

    public async Task<Group?> SetInviteAsync(ObjectId id, string inviteCode, bool active, DateTime now, CancellationToken cancellationToken) =>
        await _context.Groups.FindOneAndUpdateAsync(
            Builders<Group>.Filter.Where(x => x.Id == id && x.DeletedAt == null),
            Builders<Group>.Update.Set(x => x.InviteCode, inviteCode).Set(x => x.InviteActive, active).Set(x => x.UpdatedAt, now),
            new FindOneAndUpdateOptions<Group> { ReturnDocument = ReturnDocument.After },
            cancellationToken);

    public Task SoftDeleteAsync(ObjectId id, DateTime now, CancellationToken cancellationToken) =>
        _context.Groups.UpdateOneAsync(
            x => x.Id == id && x.DeletedAt == null,
            Builders<Group>.Update.Set(x => x.DeletedAt, now).Set(x => x.InviteActive, false).Set(x => x.MemberCount, 0).Set(x => x.UpdatedAt, now),
            cancellationToken: cancellationToken);

    public Task SetOwnerAsync(ObjectId id, ObjectId ownerId, DateTime now, CancellationToken cancellationToken) =>
        _context.Groups.UpdateOneAsync(x => x.Id == id, Builders<Group>.Update.Set(x => x.OwnerId, ownerId).Set(x => x.UpdatedAt, now), cancellationToken: cancellationToken);
}
