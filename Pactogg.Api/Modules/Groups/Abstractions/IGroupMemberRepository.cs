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
    /// <summary>Miembros activos ordenados por saldo descendente (PTS-02).</summary>
    Task<IReadOnlyList<GroupMember>> ListRankingAsync(ObjectId groupId, int limit, CancellationToken cancellationToken);
    Task CreateAsync(GroupMember member, CancellationToken cancellationToken);
    Task UpdateNickLowerAsync(ObjectId userId, string nickLower, CancellationToken cancellationToken);

    /// <summary>Membresía en cualquier estado (activa, left o removed); sirve para volver a unirse con el saldo anterior.</summary>
    Task<GroupMember?> GetAnyAsync(ObjectId groupId, ObjectId userId, CancellationToken cancellationToken);

    /// <summary>Cambia el estado (active, left, removed). Devuelve el miembro actualizado o null si no existe.</summary>
    Task<GroupMember?> SetStatusAsync(ObjectId groupId, ObjectId userId, string status, DateTime now, CancellationToken cancellationToken);

    /// <summary>MEM-03: cambia el rol de un miembro activo.</summary>
    Task<GroupMember?> SetRoleAsync(ObjectId groupId, ObjectId userId, string role, DateTime now, CancellationToken cancellationToken);

    /// <summary>GRP-05: da de baja a todos los miembros activos.</summary>
    Task DeactivateAllAsync(ObjectId groupId, DateTime now, CancellationToken cancellationToken);
}
