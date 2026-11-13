using System.ComponentModel.DataAnnotations;

namespace Pactogg.Api.Modules.Contracts.Drafts;

/// <summary>CON-08 (spec 005): la frase ya transcrita en el celular. No se envía audio.</summary>
public sealed class ParseContractRequest
{
    [Required]
    public string Phrase { get; set; } = string.Empty;
}

public sealed class DraftMemberResponse
{
    public string Id { get; init; } = string.Empty;
    public string Nickname { get; init; } = string.Empty;
}

/// <summary>Borrador que la persona revisa y edita antes de lanzarlo con CON-01 (source=voice).</summary>
public sealed class ContractDraftResponse
{
    public string TargetType { get; init; } = "group";
    public string? TargetId { get; init; }
    public string? TargetNickname { get; init; }
    public string Game { get; init; } = string.Empty;
    public string Condition { get; init; } = string.Empty;
    public string Reward { get; init; } = string.Empty;
    public IReadOnlyList<string> MissingFields { get; init; } = [];
    public bool RivalUnmatched { get; init; }
    public IReadOnlyList<DraftMemberResponse>? Members { get; init; }
    public bool Cached { get; init; }
}
