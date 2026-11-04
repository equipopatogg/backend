using System.ComponentModel.DataAnnotations;

namespace Pactogg.Api.Modules.Users.Dtos;

public sealed class UpdateProfileRequest
{
    [RegularExpression("^[A-Za-z0-9_.-]{3,24}$", ErrorMessage = "El apodo debe tener 3 a 24 caracteres: letras, números, punto, guion o guion bajo.")]
    public string? Nickname { get; set; }

    [RegularExpression("^[a-z0-9_-]{1,32}$", ErrorMessage = "avatar_id no es válido.")]
    public string? AvatarId { get; set; }
}

public sealed class UserStatsResponse
{
    public int Completed { get; init; }
    public int Cheated { get; init; }
    public int Streak { get; init; }
    public int Trophies { get; init; }
    public int WordIndex { get; init; }
}

public sealed class ProfileResponse
{
    public string Id { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Nickname { get; init; } = string.Empty;
    public string AvatarId { get; init; } = "default";
    public bool EmailVerified { get; init; }
    public UserStatsResponse Stats { get; init; } = new();
    public int GroupsCount { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed class NicknameAvailabilityResponse
{
    public string Nickname { get; init; } = string.Empty;
    public bool Available { get; init; }
}
