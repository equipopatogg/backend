using MongoDB.Bson;
using Pactogg.Api.Modules.Groups.Domain;

namespace Pactogg.Api.Modules.Groups.Abstractions;

public interface IGroupRepository
{
    Task<Group?> GetByIdAsync(ObjectId id, CancellationToken cancellationToken);
    Task<Group?> GetByInviteCodeAsync(string inviteCode, CancellationToken cancellationToken);
    Task<(IReadOnlyList<Group> Items, bool HasMore)> ListByIdsAsync(
        IReadOnlyCollection<ObjectId> ids,
        string? nameContains,
        int limit,
        ObjectId? cursor,
        CancellationToken cancellationToken);
    Task CreateAsync(Group group, CancellationToken cancellationToken);
    Task<bool> InviteCodeExistsAsync(string inviteCode, CancellationToken cancellationToken);

    /// <summary>Incrementa member_count solo si sigue por debajo del máximo (atómico). Devuelve el grupo actualizado o null si está lleno.</summary>
    Task<Group?> TryIncrementMemberCountAsync(ObjectId id, int maxMembers, DateTime at, CancellationToken cancellationToken);
    Task DecrementMemberCountAsync(ObjectId id, CancellationToken cancellationToken);
    Task DeleteAsync(ObjectId id, CancellationToken cancellationToken);
}
