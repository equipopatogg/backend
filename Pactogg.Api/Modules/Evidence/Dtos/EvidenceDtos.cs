using System.ComponentModel.DataAnnotations;

namespace Pactogg.Api.Modules.Evidence.Dtos;

public sealed class CreateLinkEvidenceRequest
{
    [Required]
    public string Type { get; set; } = "link";

    [Required, StringLength(2048, MinimumLength = 8)]
    public string Url { get; set; } = string.Empty;
}

/// <summary>Cuerpo de EVI-02/EVI-04: una foto (multipart) o un enlace (JSON).</summary>
public sealed record EvidenceUpload(byte[]? Content, string? ContentType, CreateLinkEvidenceRequest? Link);

public sealed class ModerationResponse
{
    public string Status { get; init; } = "pending";
    public IReadOnlyList<string> Labels { get; init; } = [];
    public DateTime? ModeratedAt { get; init; }
}

public sealed class EvidenceResponse
{
    public string Id { get; init; } = string.Empty;
    public string ContractId { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string? Url { get; init; }
    public string? Provider { get; init; }
    public string? ObjectKey { get; init; }
    public ModerationResponse Moderation { get; init; } = new();
    public bool SavedInFame { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? ExpiresAt { get; init; }
}
