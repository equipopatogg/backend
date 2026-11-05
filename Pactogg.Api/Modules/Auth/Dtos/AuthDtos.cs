using System.ComponentModel.DataAnnotations;

namespace Pactogg.Api.Modules.Auth.Dtos;

public sealed class RegisterRequest
{
    [Required, EmailAddress, MaxLength(254)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(128, MinimumLength = 12)]
    public string Password { get; set; } = string.Empty;

    [Required, RegularExpression("^[A-Za-z0-9_.-]{3,24}$")]
    public string Nickname { get; set; } = string.Empty;

    [Required]
    public bool AcceptPrivacy { get; set; }

    [Required, StringLength(32, MinimumLength = 1)]
    public string PrivacyVersion { get; set; } = string.Empty;

    [StringLength(128)]
    public string? DeviceId { get; set; }
}

public sealed class LoginRequest
{
    [Required, EmailAddress, MaxLength(254)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(128, MinimumLength = 1)]
    public string Password { get; set; } = string.Empty;

    [StringLength(128)]
    public string? DeviceId { get; set; }
}

/// <summary>ID 12: entrar o registrarse con Google. Para una cuenta nueva se exige apodo y aceptar la privacidad (D-15).</summary>
public sealed class GoogleAuthRequest
{
    [Required, StringLength(4096, MinimumLength = 20)]
    public string IdToken { get; set; } = string.Empty;

    [RegularExpression("^[A-Za-z0-9_.-]{3,24}$")]
    public string? Nickname { get; set; }

    public bool? AcceptPrivacy { get; set; }

    [StringLength(32)]
    public string? PrivacyVersion { get; set; }

    [StringLength(128)]
    public string? DeviceId { get; set; }
}

public sealed class LogoutRequest
{
    public string? RefreshToken { get; set; }
    public bool All { get; set; }
}

public sealed class RefreshRequest
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;

    [StringLength(128)]
    public string? DeviceId { get; set; }
}

public sealed class UserResponse
{
    public string Id { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Nickname { get; init; } = string.Empty;
    public bool EmailVerified { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed class SessionResponse
{
    public UserResponse User { get; init; } = new();
    public string AccessToken { get; init; } = string.Empty;
    public string RefreshToken { get; init; } = string.Empty;
    public string TokenType { get; init; } = "Bearer";
    public int ExpiresIn { get; init; }
}
