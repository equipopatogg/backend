using MongoDB.Bson;
using Pactogg.Api.Modules.Groups.Domain;

namespace Pactogg.Api.Modules.Groups.Abstractions;

public interface IGroupRepository
{
    Task<Group?> GetByIdAsync(ObjectId id, CancellationToken cancellationToken);
    Task CreateAsync(Group group, CancellationToken cancellationToken);
    Task<bool> InviteCodeExistsAsync(string inviteCode, CancellationToken cancellationToken);
    Task DeleteAsync(ObjectId id, CancellationToken cancellationToken);
}
