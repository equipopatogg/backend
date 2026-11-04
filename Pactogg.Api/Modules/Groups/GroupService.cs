using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using Pactogg.Api.Common;
using Pactogg.Api.Infrastructure.Audit;
using Pactogg.Api.Infrastructure.Options;
using Pactogg.Api.Infrastructure.Redis;
using Pactogg.Api.Modules.Auth.Abstractions;
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

    public GroupService(
        IGroupRepository groups,
        IGroupMemberRepository members,
        IUserRepository users,
        IAuditService audit,
        ICacheService cache,
        IOptions<AuthOptions> authOptions)
    {
        _groups = groups;
        _members = members;
        _users = users;
        _audit = audit;
        _cache = cache;
        _authOptions = authOptions.Value;
    }

    public async Task<GroupResponse> CreateAsync(ObjectId userId, CreateGroupRequest request, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken)
            ?? throw ApiException.Unauthorized();

        var activeGroups = await _members.CountActiveGroupsAsync(userId, cancellationToken);
        if (activeGroups >= _authOptions.MaxGroupsPerUser)
        {
            throw ApiException.Conflict(ErrorCodes.GroupLimitReached, $"No puedes pertenecer a más de {_authOptions.MaxGroupsPerUser} grupos.");
        }

        var settings = ValidateSettings(request.Settings);
        var now = DateTime.UtcNow;
        var group = new Group
        {
            Id = ObjectId.GenerateNewId(),
            Name = request.Name.Trim(),
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
        var group = await _groups.GetByInviteCodeAsync(code, cancellationToken)
            ?? throw ApiException.NotFound("El código no existe o ya no está activo.");

        if (await _members.GetAsync(group.Id, userId, cancellationToken) is not null)
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
        CreatedAt = group.CreatedAt
    };

    private static GroupSettings ValidateSettings(GroupSettingsRequest? request)
    {
        var jurors = request?.Jurors ?? 3;
        var consensus = request?.Consensus ?? 0.66;
        var bondOptions = request?.BondOptions ?? [25, 50, 100];

        if (jurors is < 1 or > 9)
        {
            throw ApiException.Unprocessable(ErrorCodes.ValidationError, "El número de jurados debe estar entre 1 y 9.");
        }

        if (consensus is < 0.51 or > 1.0)
        {
            throw ApiException.Unprocessable(ErrorCodes.ValidationError, "El consenso debe estar entre 0.51 y 1.0.");
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
