using MongoDB.Bson;
using Pactogg.Api.Common;
using Pactogg.Api.Modules.Groups.Abstractions;
using Pactogg.Api.Modules.Groups.Domain;

namespace Pactogg.Api.Modules.Groups;

/// <summary>Comprobación común de pertenencia: 404 si el grupo no existe, 403 si no eres miembro activo.</summary>
public sealed class GroupAccess
{
    private readonly IGroupRepository _groups;
    private readonly IGroupMemberRepository _members;

    public GroupAccess(IGroupRepository groups, IGroupMemberRepository members)
    {
        _groups = groups;
        _members = members;
    }

    public async Task<(Group Group, GroupMember Member)> RequireMemberAsync(ObjectId userId, string groupId, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(groupId, out var id))
        {
            throw ApiException.NotFound("El grupo no existe.");
        }

        return await RequireMemberAsync(userId, id, cancellationToken);
    }

    public async Task<(Group Group, GroupMember Member)> RequireMemberAsync(ObjectId userId, ObjectId groupId, CancellationToken cancellationToken)
    {
        var group = await _groups.GetByIdAsync(groupId, cancellationToken)
            ?? throw ApiException.NotFound("El grupo no existe.");
        var member = await _members.GetAsync(groupId, userId, cancellationToken)
            ?? throw ApiException.Forbidden(ErrorCodes.NotGroupMember, "No perteneces a este grupo.");
        return (group, member);
    }
}
