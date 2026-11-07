using System.ComponentModel.DataAnnotations;

namespace Pactogg.Api.Modules.Groups.Dtos;

public sealed class CreateGroupRequest
{
    [Required, StringLength(40, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    public GroupSettingsRequest? Settings { get; set; }
}

public sealed class GroupSettingsRequest
{
    [Range(1, 7)]
    public int Jurors { get; set; } = 3;

    [Range(0.50, 1.0)]
    public double Consensus { get; set; } = 0.66;

    public List<int>? BondOptions { get; set; }
}

public sealed class GroupSettingsResponse
{
    public int Jurors { get; init; }
    public double Consensus { get; init; }
    public IReadOnlyList<int> BondOptions { get; init; } = [];
}

public sealed class GroupResponse
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string OwnerId { get; init; } = string.Empty;
    public string InviteCode { get; init; } = string.Empty;
    public bool InviteActive { get; init; }
    public int MemberCount { get; init; }
    public GroupSettingsResponse Settings { get; init; } = new();
    public string MyRole { get; init; } = "admin";
    public int MyPoints { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public sealed class JoinGroupRequest
{
    [Required, RegularExpression("^[A-Za-z0-9]{6}$", ErrorMessage = "code debe tener 6 caracteres alfanuméricos.")]
    public string Code { get; set; } = string.Empty;
}

public sealed class GroupMemberResponse
{
    public string UserId { get; init; } = string.Empty;
    public string Nickname { get; init; } = string.Empty;
    public string AvatarId { get; init; } = "default";
    public string Role { get; init; } = "member";
    public int PointsBalance { get; init; }
    public DateTime JoinedAt { get; init; }
}

/// <summary>GRP-04: cambios parciales del grupo.</summary>
public sealed class UpdateGroupRequest
{
    [StringLength(40, MinimumLength = 2)]
    public string? Name { get; set; }

    public GroupSettingsRequest? Settings { get; set; }
}

/// <summary>GRP-07.</summary>
public sealed class SetInviteActiveRequest
{
    [Required]
    public bool? Active { get; set; }
}

/// <summary>MEM-03.</summary>
public sealed class SetRoleRequest
{
    [Required, RegularExpression("^(admin|member)$", ErrorMessage = "role debe ser admin o member.")]
    public string Role { get; set; } = "member";
}
