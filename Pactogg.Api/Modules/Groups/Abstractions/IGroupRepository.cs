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

    /// <summary>GRP-04: cambia nombre y ajustes.</summary>
    Task<Group?> UpdateAsync(ObjectId id, string name, GroupSettings settings, DateTime now, CancellationToken cancellationToken);

    /// <summary>GRP-06 / GRP-07: código nuevo o activo/inactivo.</summary>
    Task<Group?> SetInviteAsync(ObjectId id, string inviteCode, bool active, DateTime now, CancellationToken cancellationToken);

    /// <summary>GRP-05: borrado lógico.</summary>
    Task SoftDeleteAsync(ObjectId id, DateTime now, CancellationToken cancellationToken);

    /// <summary>USR-04: el grupo pasa a otro dueño cuando el creador elimina su cuenta.</summary>
    Task SetOwnerAsync(ObjectId id, ObjectId ownerId, DateTime now, CancellationToken cancellationToken);
}
