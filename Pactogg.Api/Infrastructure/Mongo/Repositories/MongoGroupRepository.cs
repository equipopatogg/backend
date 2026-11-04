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

    public Task CreateAsync(Group group, CancellationToken cancellationToken) =>
        _context.Groups.InsertOneAsync(group, cancellationToken: cancellationToken);

    public Task<bool> InviteCodeExistsAsync(string inviteCode, CancellationToken cancellationToken) =>
        _context.Groups.Find(x => x.InviteCode == inviteCode).AnyAsync(cancellationToken);

    public Task DeleteAsync(ObjectId id, CancellationToken cancellationToken) =>
        _context.Groups.DeleteOneAsync(x => x.Id == id, cancellationToken);
}
