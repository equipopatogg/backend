using MongoDB.Bson;
using Pactogg.Api.Modules.Groups.Domain;

namespace Pactogg.Api.Modules.Groups.Abstractions;

public interface IGroupMemberRepository
{
    Task<GroupMember?> GetAsync(ObjectId groupId, ObjectId userId, CancellationToken cancellationToken);
    Task<int> CountActiveGroupsAsync(ObjectId userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ObjectId>> ListActiveGroupIdsAsync(ObjectId userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<GroupMember>> ListForUserAsync(ObjectId userId, CancellationToken cancellationToken);

    /// <summary>Miembros activos en orden de ingreso; nickPrefix filtra por prefijo de nick_lower.</summary>
    Task<(IReadOnlyList<GroupMember> Items, bool HasMore)> ListByGroupAsync(
        ObjectId groupId,
        string? nickPrefix,
        int limit,
        ObjectId? cursor,
        CancellationToken cancellationToken);
    Task CreateAsync(GroupMember member, CancellationToken cancellationToken);
    Task UpdateNickLowerAsync(ObjectId userId, string nickLower, CancellationToken cancellationToken);
}
