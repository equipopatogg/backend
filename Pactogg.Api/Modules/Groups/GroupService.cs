using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using Pactogg.Api.Common;
using Pactogg.Api.Infrastructure.Audit;
using Pactogg.Api.Infrastructure.Moderation;
using Pactogg.Api.Infrastructure.Options;
using Pactogg.Api.Infrastructure.Redis;
using Pactogg.Api.Modules.Auth.Abstractions;
using Pactogg.Api.Modules.Contracts.Abstractions;
using Pactogg.Api.Modules.Notifications;
using Pactogg.Api.Modules.Groups.Abstractions;
using Pactogg.Api.Modules.Groups.Domain;
using Pactogg.Api.Modules.Groups.Dtos;

namespace Pactogg.Api.Modules.Groups;

public sealed class GroupService
{
    private const string InviteAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private readonly IGroupRepository _groups;
    private readonly IGroupMemberRepository _members;
    private readonly IUserRepository _users;
    private readonly IAuditService _audit;
    private readonly ICacheService _cache;
    private readonly AuthOptions _authOptions;
    private readonly IModerationService _moderation;
    private readonly IContractRepository _contracts;
    private readonly NotificationService _notifications;

    public GroupService(
        IGroupRepository groups,
        IGroupMemberRepository members,
        IUserRepository users,
        IAuditService audit,
        ICacheService cache,
        IOptions<AuthOptions> authOptions,
        IModerationService moderation,
        IContractRepository contracts,
        NotificationService notifications)
    {
        _groups = groups;
        _members = members;
        _users = users;
        _audit = audit;
        _cache = cache;
        _authOptions = authOptions.Value;
        _moderation = moderation;
        _contracts = contracts;
        _notifications = notifications;
    }

    /// <summary>CLARIFICACION #5: en cuentas de correo, la verificación es obligatoria para crear o unirse a un grupo.</summary>
    private void RequireVerified(Pactogg.Api.Modules.Auth.Domain.User user)
    {
        if (_authOptions.RequireVerifiedEmail && !user.EmailVerified)
        {
            throw ApiException.Forbidden(ErrorCodes.EmailNotVerified, "Verifica tu correo para crear o unirte a un grupo.");
        }
    }

    public async Task<GroupResponse> CreateAsync(ObjectId userId, CreateGroupRequest request, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken)
            ?? throw ApiException.Unauthorized();
        RequireVerified(user);

        var activeGroups = await _members.CountActiveGroupsAsync(userId, cancellationToken);
        if (activeGroups >= _authOptions.MaxGroupsPerUser)
        {
            throw ApiException.Conflict(ErrorCodes.GroupLimitReached, $"No puedes pertenecer a más de {_authOptions.MaxGroupsPerUser} grupos.");
        }

        var settings = ValidateSettings(request.Settings);
        var name = request.Name.Trim();
        // Q-021: el nombre del grupo pasa por moderación de texto antes de guardarse; si el moderador falla, la operación falla (falla cerrada).
        var moderation = await _moderation.ModerateTextAsync(name, cancellationToken);
        if (!moderation.Approved)
        {
            throw ApiException.Unprocessable(ErrorCodes.ContentRejected, "El nombre del grupo fue rechazado por moderación.", new { labels = moderation.Labels });
        }

        var now = DateTime.UtcNow;
        var group = new Group
        {
            Id = ObjectId.GenerateNewId(),
            Name = name,
            OwnerId = userId,
            InviteCode = await GenerateInviteCodeAsync(cancellationToken),
            InviteActive = true,
            Settings = settings,
            MemberCount = 1,
            CurrentSeason = 1,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _groups.CreateAsync(group, cancellationToken);
        try
        {
            await _members.CreateAsync(new GroupMember
            {
                Id = ObjectId.GenerateNewId(),
                GroupId = group.Id,
                UserId = userId,
                Role = "admin",
                NickLower = user.NicknameLower,
                PointsBalance = _authOptions.InitialPoints,
                Status = "active",
                JoinedAt = now,
                UpdatedAt = now
            }, cancellationToken);
        }
        catch
        {
            await _groups.DeleteAsync(group.Id, cancellationToken);
            throw;
        }

        await _audit.AppendAsync("group.created", userId, "group", group.Id, group.Id, new { group_id = group.Id.ToString() }, cancellationToken);
        await _cache.BumpVersionAsync(CacheExtensions.UserScope(userId), cancellationToken);

        return ToResponse(group, "admin", _authOptions.InitialPoints);
    }

    /// <summary>GRP-02: grupos del usuario (q por nombre, cursor). Caché 60 s invalidada al crear o unirse.</summary>
    public async Task<(IReadOnlyList<GroupResponse> Items, object Meta)> ListAsync(
        ObjectId userId,
        string? q,
        int limit,
        string? cursor,
        CancellationToken cancellationToken)
    {
        ListQuery.ValidateLimit(limit);
        var cursorId = ListQuery.ParseCursor(cursor);
        var query = ListQuery.NormalizeText(q, 40, "q");

        var version = await _cache.GetVersionAsync(CacheExtensions.UserScope(userId), cancellationToken);
        var snapshot = await _cache.GetOrCreateJsonAsync(
            $"groups:list:{userId}:{version}:{query}:{limit}:{cursor}",
            TimeSpan.FromSeconds(60),
            async () =>
            {
                var memberships = await _members.ListForUserAsync(userId, cancellationToken);
                var byGroup = memberships.GroupBy(x => x.GroupId).ToDictionary(x => x.Key, x => x.First());
                var (items, hasMore) = await _groups.ListByIdsAsync(byGroup.Keys.ToArray(), query, limit, cursorId, cancellationToken);
                var responses = items
                    .Select(group => ToResponse(group, byGroup[group.Id].Role, byGroup[group.Id].PointsBalance))
                    .ToList();
                var next = hasMore && items.Count > 0 ? items[^1].Id.ToString() : null;
                return new PageSnapshot<GroupResponse>(responses, next, hasMore);
            },
            cancellationToken);

        return (snapshot.Items, snapshot.Meta);
    }

    /// <summary>GRP-03: detalle con configuración; solo miembros.</summary>
    public async Task<GroupResponse> GetAsync(ObjectId userId, string id, CancellationToken cancellationToken)
    {
        var (group, member) = await RequireMembershipAsync(userId, id, cancellationToken);
        return ToResponse(group, member.Role, member.PointsBalance);
    }

    /// <summary>MEM-01: unirse con código. Máximo 20 miembros por grupo y 5 grupos por usuario.</summary>
    public async Task<GroupResponse> JoinAsync(ObjectId userId, JoinGroupRequest request, CancellationToken cancellationToken)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        var user = await _users.GetByIdAsync(userId, cancellationToken) ?? throw ApiException.Unauthorized();
        RequireVerified(user);
        var group = await _groups.GetByInviteCodeAsync(code, cancellationToken)
            ?? throw ApiException.NotFound("El código no existe o ya no está activo.");

        var previous = await _members.GetAnyAsync(group.Id, userId, cancellationToken);
        if (previous is { Status: "active" })
        {
            throw ApiException.Conflict(ErrorCodes.AlreadyMember, "Ya perteneces a este grupo.");
        }

        if (await _members.CountActiveGroupsAsync(userId, cancellationToken) >= _authOptions.MaxGroupsPerUser)
        {
            throw ApiException.Conflict(ErrorCodes.GroupLimitReached, $"No puedes pertenecer a más de {_authOptions.MaxGroupsPerUser} grupos.");
        }

        var now = DateTime.UtcNow;
        var updated = await _groups.TryIncrementMemberCountAsync(group.Id, _authOptions.MaxMembersPerGroup, now, cancellationToken)
            ?? throw ApiException.Conflict(ErrorCodes.GroupFull, $"El grupo ya tiene {_authOptions.MaxMembersPerGroup} miembros.");

        if (previous is not null)
        {
            // ADM-R8: quien salió o fue expulsado vuelve con el saldo que tenía (CLARIFICACION #26).
            var back = await _members.SetStatusAsync(group.Id, userId, "active", now, cancellationToken);
            await _audit.AppendAsync("group.member_rejoined", userId, "group", group.Id, group.Id, new { group_id = group.Id.ToString() }, cancellationToken);
            await _cache.BumpVersionAsync(CacheExtensions.UserScope(userId), cancellationToken);
            await _cache.BumpVersionAsync(CacheExtensions.GroupScope(group.Id), cancellationToken);
            return ToResponse(updated, "member", back?.PointsBalance ?? previous.PointsBalance);
        }

        try
        {
            await _members.CreateAsync(new GroupMember
            {
                Id = ObjectId.GenerateNewId(),
                GroupId = group.Id,
                UserId = userId,
                Role = "member",
                NickLower = user.NicknameLower,
                PointsBalance = _authOptions.InitialPoints,
                Status = "active",
                JoinedAt = now,
                UpdatedAt = now
            }, cancellationToken);
        }
        catch
        {
            await _groups.DecrementMemberCountAsync(group.Id, cancellationToken);
            throw;
        }

        await _audit.AppendAsync("group.member_joined", userId, "group", group.Id, group.Id, new { group_id = group.Id.ToString() }, cancellationToken);
        await _cache.BumpVersionAsync(CacheExtensions.UserScope(userId), cancellationToken);
        await _cache.BumpVersionAsync(CacheExtensions.GroupScope(group.Id), cancellationToken);

        return ToResponse(updated, "member", _authOptions.InitialPoints);
    }

    /// <summary>MEM-02: miembros con rol y saldo; q = prefijo del apodo (autocompletado de rival).</summary>
    public async Task<(IReadOnlyList<GroupMemberResponse> Items, object Meta)> ListMembersAsync(
        ObjectId userId,
        string id,
        string? q,
        int limit,
        string? cursor,
        CancellationToken cancellationToken)
    {
        ListQuery.ValidateLimit(limit);
        var cursorId = ListQuery.ParseCursor(cursor);
        var prefix = ListQuery.NormalizeText(q, 24, "q")?.ToLowerInvariant();
        var (group, _) = await RequireMembershipAsync(userId, id, cancellationToken);

        var version = await _cache.GetVersionAsync(CacheExtensions.GroupScope(group.Id), cancellationToken);
        var snapshot = await _cache.GetOrCreateJsonAsync(
            $"groups:members:{group.Id}:{version}:{prefix}:{limit}:{cursor}",
            TimeSpan.FromSeconds(60),
            async () =>
            {
                var (items, hasMore) = await _members.ListByGroupAsync(group.Id, prefix, limit, cursorId, cancellationToken);
                var users = (await _users.ListByIdsAsync(items.Select(x => x.UserId).ToArray(), cancellationToken))
                    .ToDictionary(x => x.Id);
                var responses = items.Select(member =>
                {
                    users.TryGetValue(member.UserId, out var user);
                    return new GroupMemberResponse
                    {
                        UserId = member.UserId.ToString(),
                        Nickname = user?.Nickname ?? member.NickLower,
                        AvatarId = user?.AvatarId ?? "default",
                        Role = member.Role,
                        PointsBalance = member.PointsBalance,
                        JoinedAt = member.JoinedAt
                    };
                }).ToList();
                var next = hasMore && items.Count > 0 ? items[^1].Id.ToString() : null;
                return new PageSnapshot<GroupMemberResponse>(responses, next, hasMore);
            },
            cancellationToken);

        return (snapshot.Items, snapshot.Meta);
    }

    private async Task<(Group Group, GroupMember Member)> RequireMembershipAsync(ObjectId userId, string id, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(id, out var groupId))
        {
            throw ApiException.NotFound("El grupo no existe.");
        }

        var group = await _groups.GetByIdAsync(groupId, cancellationToken)
            ?? throw ApiException.NotFound("El grupo no existe.");
        var member = await _members.GetAsync(groupId, userId, cancellationToken)
            ?? throw ApiException.Forbidden(ErrorCodes.NotGroupMember, "No perteneces a este grupo.");
        return (group, member);
    }

    // ---------- Administración del grupo (GRP-04..07, MEM-03..06) ----------

    private async Task<(Group Group, GroupMember Member)> RequireAdminAsync(ObjectId userId, string id, CancellationToken cancellationToken)
    {
        var (group, member) = await RequireMembershipAsync(userId, id, cancellationToken);
        if (member.Role != "admin")
        {
            throw ApiException.Forbidden(ErrorCodes.NotGroupAdmin, "Solo un administrador del grupo puede hacer esto.");
        }

        return (group, member);
    }

    private async Task InvalidateAsync(Group group, params ObjectId[] users)
    {
        await _cache.BumpVersionAsync(CacheExtensions.GroupScope(group.Id), CancellationToken.None);
        foreach (var user in users)
        {
            await _cache.BumpVersionAsync(CacheExtensions.UserScope(user), CancellationToken.None);
        }
    }

    /// <summary>GRP-04: nombre (moderado) y reglas del jurado. Mismas reglas que GRP-01.</summary>
    public async Task<GroupResponse> UpdateAsync(ObjectId userId, string id, UpdateGroupRequest request, CancellationToken cancellationToken)
    {
        var (group, member) = await RequireAdminAsync(userId, id, cancellationToken);
        if (request.Name is null && request.Settings is null)
        {
            throw ApiException.Unprocessable(ErrorCodes.ValidationError, "Envía name o settings.");
        }

        var name = request.Name?.Trim() ?? group.Name;
        if (request.Name is not null)
        {
            var moderation = await _moderation.ModerateTextAsync(name, cancellationToken);
            if (!moderation.Approved)
            {
                throw ApiException.Unprocessable(ErrorCodes.ContentRejected, "El nombre del grupo fue rechazado por moderación.", new { labels = moderation.Labels });
            }
        }

        var settings = request.Settings is null ? group.Settings : ValidateSettings(request.Settings);
        var updated = await _groups.UpdateAsync(group.Id, name, settings, DateTime.UtcNow, cancellationToken)
            ?? throw ApiException.NotFound("El grupo no existe.");
        await _audit.AppendAsync("group.updated", userId, "group", group.Id, group.Id, new { name_changed = request.Name is not null, settings_changed = request.Settings is not null }, cancellationToken);
        await InvalidateAsync(group);
        return ToResponse(updated, member.Role, member.PointsBalance);
    }

    /// <summary>GRP-06: código nuevo; el anterior deja de funcionar.</summary>
    public async Task<GroupResponse> RegenerateInviteAsync(ObjectId userId, string id, CancellationToken cancellationToken)
    {
        var (group, member) = await RequireAdminAsync(userId, id, cancellationToken);
        var code = await GenerateInviteCodeAsync(cancellationToken);
        var updated = await _groups.SetInviteAsync(group.Id, code, true, DateTime.UtcNow, cancellationToken)
            ?? throw ApiException.NotFound("El grupo no existe.");
        await _audit.AppendAsync("group.invite_regenerated", userId, "group", group.Id, group.Id, new { group_id = group.Id.ToString() }, cancellationToken);
        await InvalidateAsync(group);
        return ToResponse(updated, member.Role, member.PointsBalance);
    }

    /// <summary>GRP-07: activa o desactiva el código.</summary>
    public async Task<GroupResponse> SetInviteActiveAsync(ObjectId userId, string id, SetInviteActiveRequest request, CancellationToken cancellationToken)
    {
        var (group, member) = await RequireAdminAsync(userId, id, cancellationToken);
        var updated = await _groups.SetInviteAsync(group.Id, group.InviteCode, request.Active!.Value, DateTime.UtcNow, cancellationToken)
            ?? throw ApiException.NotFound("El grupo no existe.");
        await _audit.AppendAsync("group.invite_toggled", userId, "group", group.Id, group.Id, new { active = request.Active }, cancellationToken);
        await InvalidateAsync(group);
        return ToResponse(updated, member.Role, member.PointsBalance);
    }

    /// <summary>GRP-05: solo el creador; cancela retos activos con devolución, da de baja a los miembros y borra lógicamente.</summary>
    public async Task DeleteAsync(ObjectId userId, string id, CancellationToken cancellationToken)
    {
        var (group, _) = await RequireMembershipAsync(userId, id, cancellationToken);
        if (group.OwnerId != userId)
        {
            throw ApiException.Forbidden(ErrorCodes.NotGroupOwner, "Solo el creador puede eliminar el grupo.");
        }

        var now = DateTime.UtcNow;
        var cancelled = 0;
        foreach (var contract in await _contracts.ListActiveAsync(group.Id, null, cancellationToken))
        {
            if (await _contracts.TryCancelActiveWithRefundAsync(contract.Id, "group_deleted", now, cancellationToken) is not null)
            {
                cancelled++;
            }
        }

        var (members, _) = await _members.ListByGroupAsync(group.Id, null, 50, null, cancellationToken);
        await _members.DeactivateAllAsync(group.Id, now, cancellationToken);
        await _groups.SoftDeleteAsync(group.Id, now, cancellationToken);
        await _audit.AppendAsync("group.deleted", userId, "group", group.Id, group.Id, new { cancelled_contracts = cancelled }, cancellationToken);
        await InvalidateAsync(group, members.Select(x => x.UserId).ToArray());
    }

    /// <summary>MEM-03: cambiar rol. El creador no se puede degradar.</summary>
    public async Task<GroupMemberResponse> SetRoleAsync(ObjectId userId, string id, string targetUserId, SetRoleRequest request, CancellationToken cancellationToken)
    {
        var (group, _) = await RequireAdminAsync(userId, id, cancellationToken);
        var target = ParseUser(targetUserId);
        if (target == group.OwnerId)
        {
            throw ApiException.Conflict(ErrorCodes.CannotModifyOwner, "El creador del grupo no se puede degradar ni expulsar.");
        }

        var updated = await _members.SetRoleAsync(group.Id, target, request.Role, DateTime.UtcNow, cancellationToken)
            ?? throw ApiException.NotFound("Esa persona no es miembro del grupo.");
        await _audit.AppendAsync("group.role_changed", userId, "group_member", updated.Id, group.Id, new { user_id = target.ToString(), role = request.Role }, cancellationToken);
        await _notifications.NotifyAsync(
            [target],
            "group_role_changed",
            request.Role == "admin" ? $"Ahora eres administrador de {group.Name}" : $"Ya no eres administrador de {group.Name}",
            "Tu rol en el grupo cambió.",
            group.Id,
            cancellationToken: cancellationToken,
            dedupeSuffix: $"{request.Role}:{DateTime.UtcNow.Ticks}");
        await InvalidateAsync(group, target);
        var user = await _users.GetByIdAsync(target, cancellationToken);
        return ToMemberResponse(updated, user);
    }

    /// <summary>MEM-04: expulsar. No al creador ni a uno mismo (para eso MEM-05).</summary>
    public async Task RemoveMemberAsync(ObjectId userId, string id, string targetUserId, CancellationToken cancellationToken)
    {
        var (group, _) = await RequireAdminAsync(userId, id, cancellationToken);
        var target = ParseUser(targetUserId);
        if (target == userId)
        {
            throw ApiException.Unprocessable(ErrorCodes.ValidationError, "Para salir del grupo usa DELETE /groups/{id}/members/me.");
        }

        if (target == group.OwnerId)
        {
            throw ApiException.Conflict(ErrorCodes.CannotModifyOwner, "El creador del grupo no se puede degradar ni expulsar.");
        }

        if (await _members.GetAsync(group.Id, target, cancellationToken) is null)
        {
            throw ApiException.NotFound("Esa persona no es miembro del grupo.");
        }

        await DepartAsync(group, target, "removed", "member_removed", cancellationToken);
        await _audit.AppendAsync("group.member_removed", userId, "group", group.Id, group.Id, new { user_id = target.ToString() }, cancellationToken);
        await _notifications.NotifyAsync(
            [target],
            "group_member_removed",
            $"Te sacaron de {group.Name}",
            "Tus retos activos en ese grupo se cancelaron y se devolvieron las fianzas.",
            group.Id,
            cancellationToken: cancellationToken,
            dedupeSuffix: DateTime.UtcNow.Ticks.ToString());
    }

    /// <summary>MEM-05: salir del grupo. El creador no puede salir (ADM-D1).</summary>
    public async Task LeaveAsync(ObjectId userId, string id, CancellationToken cancellationToken)
    {
        var (group, _) = await RequireMembershipAsync(userId, id, cancellationToken);
        if (group.OwnerId == userId)
        {
            throw ApiException.Conflict(ErrorCodes.OwnerCannotLeave, "El creador no puede salir; puede eliminar el grupo.");
        }

        await DepartAsync(group, userId, "left", "member_left", cancellationToken);
        await _audit.AppendAsync("group.member_left", userId, "group", group.Id, group.Id, new { user_id = userId.ToString() }, cancellationToken);
    }

    /// <summary>MEM-06: mi rol y saldo en el grupo.</summary>
    public async Task<GroupMemberResponse> GetMyMembershipAsync(ObjectId userId, string id, CancellationToken cancellationToken)
    {
        var (_, member) = await RequireMembershipAsync(userId, id, cancellationToken);
        var user = await _users.GetByIdAsync(userId, cancellationToken);
        return ToMemberResponse(member, user);
    }

    /// <summary>USR-04: sale de todos sus grupos. Si era el creador, el grupo pasa al administrador (o miembro) más antiguo; si no queda nadie, se elimina (USR04-D1).</summary>
    public async Task RemoveForAccountDeletionAsync(ObjectId userId, CancellationToken cancellationToken)
    {
        foreach (var membership in await _members.ListForUserAsync(userId, cancellationToken))
        {
            var group = await _groups.GetByIdAsync(membership.GroupId, cancellationToken);
            if (group is null)
            {
                continue;
            }

            if (group.OwnerId == userId)
            {
                var (others, _) = await _members.ListByGroupAsync(group.Id, null, 50, null, cancellationToken);
                var heir = others.Where(x => x.UserId != userId)
                    .OrderByDescending(x => x.Role == "admin")
                    .ThenBy(x => x.JoinedAt)
                    .FirstOrDefault();
                if (heir is null)
                {
                    await DeleteAsync(userId, group.Id.ToString(), cancellationToken);
                    continue;
                }

                var now = DateTime.UtcNow;
                await _groups.SetOwnerAsync(group.Id, heir.UserId, now, cancellationToken);
                await _members.SetRoleAsync(group.Id, heir.UserId, "admin", now, cancellationToken);
                await _audit.AppendAsync("group.owner_transferred", userId, "group", group.Id, group.Id, new { new_owner = heir.UserId.ToString() }, cancellationToken);
                await _notifications.NotifyAsync([heir.UserId], "group_owner_transferred", $"Ahora eres el creador de {group.Name}", "La persona que lo creó eliminó su cuenta.", group.Id, cancellationToken: cancellationToken);
            }

            await DepartAsync(group, userId, "deleted", "account_deleted", cancellationToken);
        }
    }

    /// <summary>ADM-R6: cancela los retos activos de quien se va (con devolución de fianzas) y lo da de baja conservando su saldo.</summary>
    private async Task DepartAsync(Group group, ObjectId target, string status, string reason, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        foreach (var contract in await _contracts.ListActiveAsync(group.Id, target, cancellationToken))
        {
            await _contracts.TryCancelActiveWithRefundAsync(contract.Id, reason, now, cancellationToken);
        }

        await _members.SetStatusAsync(group.Id, target, status, now, cancellationToken);
        await _groups.DecrementMemberCountAsync(group.Id, cancellationToken);
        await InvalidateAsync(group, target);
    }

    private static ObjectId ParseUser(string id) =>
        ObjectId.TryParse(id, out var value) ? value : throw ApiException.NotFound("Esa persona no es miembro del grupo.");

    private static GroupMemberResponse ToMemberResponse(GroupMember member, Pactogg.Api.Modules.Auth.Domain.User? user) => new()
    {
        UserId = member.UserId.ToString(),
        Nickname = user?.Nickname ?? member.NickLower,
        AvatarId = user?.AvatarId ?? "default",
        Role = member.Role,
        PointsBalance = member.PointsBalance,
        JoinedAt = member.JoinedAt
    };

    private static GroupResponse ToResponse(Group group, string myRole, int myPoints) => new()
    {
        Id = group.Id.ToString(),
        Name = group.Name,
        OwnerId = group.OwnerId.ToString(),
        InviteCode = group.InviteCode,
        InviteActive = group.InviteActive,
        MemberCount = group.MemberCount,
        Settings = new GroupSettingsResponse
        {
            Jurors = group.Settings.Jurors,
            Consensus = group.Settings.Consensus,
            BondOptions = group.Settings.BondOptions
        },
        MyRole = myRole,
        MyPoints = myPoints,
        CreatedAt = group.CreatedAt,
        UpdatedAt = group.UpdatedAt
    };

    private static GroupSettings ValidateSettings(GroupSettingsRequest? request)
    {
        var jurors = request?.Jurors ?? 3;
        var consensus = request?.Consensus ?? 0.66;
        var bondOptions = request?.BondOptions ?? [25, 50, 100];

        if (jurors is < 1 or > 7)
        {
            throw ApiException.Unprocessable(ErrorCodes.ValidationError, "El número de jurados debe estar entre 1 y 7.");
        }

        if (consensus is < 0.50 or > 1.0)
        {
            throw ApiException.Unprocessable(ErrorCodes.ValidationError, "El consenso debe estar entre 0.50 y 1.0.");
        }

        if (bondOptions.Count is < 1 or > 5 || bondOptions.Any(value => value is < 10 or > 200) || bondOptions.Distinct().Count() != bondOptions.Count)
        {
            throw ApiException.Unprocessable(ErrorCodes.ValidationError, "Las fianzas deben ser 1 a 5 valores únicos entre 10 y 200.");
        }

        return new GroupSettings
        {
            Jurors = jurors,
            Consensus = consensus,
            BondOptions = bondOptions
        };
    }

    private async Task<string> GenerateInviteCodeAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var bytes = RandomNumberGenerator.GetBytes(6);
            var code = string.Create(6, bytes, static (span, random) =>
            {
                for (var i = 0; i < span.Length; i++)
                {
                    span[i] = InviteAlphabet[random[i] % InviteAlphabet.Length];
                }
            });

            if (!await _groups.InviteCodeExistsAsync(code, cancellationToken))
            {
                return code;
            }
        }

        throw ApiException.ServiceUnavailable(ErrorCodes.InternalError, "No se pudo generar el código de invitación.");
    }
}
