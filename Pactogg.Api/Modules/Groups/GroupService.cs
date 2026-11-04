using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using Pactogg.Api.Common;
using Pactogg.Api.Infrastructure.Audit;
using Pactogg.Api.Infrastructure.Options;
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
    private readonly AuthOptions _authOptions;

    public GroupService(
        IGroupRepository groups,
        IGroupMemberRepository members,
        IUserRepository users,
        IAuditService audit,
        IOptions<AuthOptions> authOptions)
    {
        _groups = groups;
        _members = members;
        _users = users;
        _audit = audit;
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

        return new GroupResponse
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
            MyRole = "admin",
            MyPoints = _authOptions.InitialPoints,
            CreatedAt = group.CreatedAt
        };
    }

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
