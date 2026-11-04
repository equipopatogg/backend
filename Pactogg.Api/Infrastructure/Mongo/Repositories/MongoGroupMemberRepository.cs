using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;
using Pactogg.Api.Common;
using Pactogg.Api.Modules.Groups.Abstractions;
using Pactogg.Api.Modules.Groups.Domain;

namespace Pactogg.Api.Infrastructure.Mongo.Repositories;

public sealed class MongoGroupMemberRepository : IGroupMemberRepository
{
    private readonly MongoContext _context;

    public MongoGroupMemberRepository(MongoContext context)
    {
        _context = context;
    }

    public async Task<GroupMember?> GetAsync(ObjectId groupId, ObjectId userId, CancellationToken cancellationToken) =>
        await _context.GroupMembers.Find(x => x.GroupId == groupId && x.UserId == userId && x.Status == "active")
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<int> CountActiveGroupsAsync(ObjectId userId, CancellationToken cancellationToken) =>
        (int)await _context.GroupMembers.CountDocumentsAsync(
            x => x.UserId == userId && x.Status == "active",
            cancellationToken: cancellationToken);

    public async Task<IReadOnlyList<ObjectId>> ListActiveGroupIdsAsync(ObjectId userId, CancellationToken cancellationToken)
    {
        var ids = await _context.GroupMembers.Find(x => x.UserId == userId && x.Status == "active")
            .Project(x => x.GroupId)
            .ToListAsync(cancellationToken);
        return ids;
    }

    public async Task<IReadOnlyList<GroupMember>> ListForUserAsync(ObjectId userId, CancellationToken cancellationToken) =>
        await _context.GroupMembers.Find(x => x.UserId == userId && x.Status == "active")
            .ToListAsync(cancellationToken);

    public async Task<(IReadOnlyList<GroupMember> Items, bool HasMore)> ListByGroupAsync(
        ObjectId groupId,
        string? nickPrefix,
        int limit,
        ObjectId? cursor,
        CancellationToken cancellationToken)
    {
        var builder = Builders<GroupMember>.Filter;
        var filter = builder.Eq(x => x.GroupId, groupId) & builder.Eq(x => x.Status, "active");
        if (!string.IsNullOrWhiteSpace(nickPrefix))
        {
            // Prefijo anclado y escapado: usa ix_group_members_group_nick y evita inyección de regex.
            filter &= builder.Regex(x => x.NickLower, new BsonRegularExpression("^" + Regex.Escape(nickPrefix.ToLowerInvariant())));
        }

        if (cursor.HasValue)
        {
            filter &= builder.Gt(x => x.Id, cursor.Value);
        }

        var items = await _context.GroupMembers.Find(filter)
            .SortBy(x => x.Id)
            .Limit(limit + 1)
            .ToListAsync(cancellationToken);

        return (items.Take(limit).ToList(), items.Count > limit);
    }

    public async Task CreateAsync(GroupMember member, CancellationToken cancellationToken)
    {
        try
        {
            await _context.GroupMembers.InsertOneAsync(member, cancellationToken: cancellationToken);
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            throw ApiException.Conflict(ErrorCodes.AlreadyMember, "Ya perteneces a este grupo.");
        }
    }

    public Task UpdateNickLowerAsync(ObjectId userId, string nickLower, CancellationToken cancellationToken) =>
        _context.GroupMembers.UpdateManyAsync(
            x => x.UserId == userId,
            Builders<GroupMember>.Update.Set(x => x.NickLower, nickLower).Set(x => x.UpdatedAt, DateTime.UtcNow),
            cancellationToken: cancellationToken);
}
