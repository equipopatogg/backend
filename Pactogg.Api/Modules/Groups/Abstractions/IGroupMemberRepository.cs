using MongoDB.Bson;
using Pactogg.Api.Modules.Groups.Domain;

namespace Pactogg.Api.Modules.Groups.Abstractions;

public interface IGroupMemberRepository
{
    Task<GroupMember?> GetAsync(ObjectId groupId, ObjectId userId, CancellationToken cancellationToken);
    Task<int> CountActiveGroupsAsync(ObjectId userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ObjectId>> ListActiveGroupIdsAsync(ObjectId userId, CancellationToken cancellationToken);
    Task CreateAsync(GroupMember member, CancellationToken cancellationToken);
}
