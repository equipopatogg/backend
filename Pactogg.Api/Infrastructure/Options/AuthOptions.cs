namespace Pactogg.Api.Infrastructure.Options;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public string CurrentPrivacyVersion { get; set; } = "2026-10-01";
    public int MaxGroupsPerUser { get; set; } = 5;
    public int MaxMembersPerGroup { get; set; } = 20;
    public int InitialPoints { get; set; } = 100;
    /// <summary>Client IDs de OAuth de Google aceptados como "aud" (Android y Web). Vacío: login con Google deshabilitado (503).</summary>
    public List<string> GoogleClientIds { get; set; } = [];
    public int MaxFailedAttempts { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 15;
}
