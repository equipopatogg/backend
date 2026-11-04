using MongoDB.Bson;
using MongoDB.Driver;
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

    public Task CreateAsync(GroupMember member, CancellationToken cancellationToken) =>
        _context.GroupMembers.InsertOneAsync(member, cancellationToken: cancellationToken);
}
