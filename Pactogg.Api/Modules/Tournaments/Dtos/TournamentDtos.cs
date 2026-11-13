using System.ComponentModel.DataAnnotations;

namespace Pactogg.Api.Modules.Tournaments.Dtos;

/// <summary>TOR-01 (010-R1).</summary>
public sealed class CreateTournamentRequest
{
    [Required, StringLength(60, MinimumLength = 1)]
    public string Game { get; set; } = string.Empty;

    /// <summary>Solo single_elimination (spec 010, fuera de alcance otros formatos).</summary>
    public string? Format { get; set; }

    [Required]
    public int? MaxGroups { get; set; }

    [Range(0, 500)]
    public int EntryPoints { get; set; }

    [Required]
    public DateTime? ScheduledAt { get; set; }
}

/// <summary>TOR-04 (010-R3): todos los campos son opcionales; la inscripción no se cambia.</summary>
public sealed class UpdateTournamentRequest
{
    [StringLength(60, MinimumLength = 1)]
    public string? Game { get; set; }

    public DateTime? ScheduledAt { get; set; }

    public int? MaxGroups { get; set; }
}

/// <summary>TOR-06 (010-R5, 010-D3).</summary>
public sealed class JoinTournamentRequest
{
    [Required]
    public string GroupId { get; set; } = string.Empty;

    /// <summary>Obligatorio si la ruta lleva el id del torneo; si la ruta ya es el código, se puede omitir.</summary>
    [StringLength(6, MinimumLength = 6)]
    public string? Code { get; set; }
}

public sealed class MatchResponse
{
    public string Id { get; init; } = string.Empty;
    public string TournamentId { get; init; } = string.Empty;
    public int Round { get; init; }
    public int Position { get; init; }
    public string? P1GroupId { get; init; }
    public string? P2GroupId { get; init; }
    public int? P1Score { get; init; }
    public int? P2Score { get; init; }
    public string? WinnerGroupId { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? NextMatchId { get; init; }
    public DateTime? ReportedAt { get; init; }
    public string? ReportedByGroupId { get; init; }
    public string? DisputeReason { get; init; }
    /// <summary>confirmed_by_rival, resolved_dispute, resolved_no_answer o abandonment.</summary>
    public string? Resolution { get; init; }
    public DateTime? ConfirmedAt { get; init; }
    /// <summary>URL firmada (15 min) de la foto del resultado; solo en MAT-02..06.</summary>
    public string? ScreenshotUrl { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public sealed class BracketResponse
{
    /// <summary>Lugares del bracket (potencia de 2); 0 mientras el torneo no inicia.</summary>
    public int Size { get; init; }
    public int Rounds { get; init; }
    public IReadOnlyList<MatchResponse> Matches { get; init; } = [];
}

/// <summary>Grupo inscrito: solo nombre y número de miembros, sin datos personales (010-R2).</summary>
public sealed class TournamentGroupResponse
{
    public string GroupId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public int MemberCount { get; init; }
    public bool IsOrganizer { get; init; }
}

public sealed class TournamentResponse
{
    public string Id { get; init; } = string.Empty;
    public string GroupId { get; init; } = string.Empty;
    public string OrganizerId { get; init; } = string.Empty;
    public string Game { get; init; } = string.Empty;
    public string Format { get; init; } = string.Empty;
    public int MaxGroups { get; init; }
    public int EntryPoints { get; init; }
    public int PrizePool { get; init; }
    public DateTime ScheduledAt { get; init; }
    public IReadOnlyList<string> Participants { get; init; } = [];
    public IReadOnlyList<TournamentGroupResponse>? Groups { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? WinnerGroupId { get; init; }
    /// <summary>Solo para el organizador y los administradores de grupos inscritos.</summary>
    public string? Code { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CancelledAt { get; init; }
    public string? CancelReason { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public BracketResponse Bracket { get; init; } = new();
}

/// <summary>MAT-07: siguiente partida de alguno de mis grupos.</summary>
public sealed class NextMatchResponse
{
    public MatchResponse Match { get; init; } = new();
    public string MyGroupId { get; init; } = string.Empty;
    public string? OpponentGroupId { get; init; }
    public string Game { get; init; } = string.Empty;
    public DateTime ScheduledAt { get; init; }
}

/// <summary>MAT-05 (010-R12): motivo de la disputa.</summary>
public sealed class DisputeMatchRequest
{
    [Required, StringLength(140, MinimumLength = 5)]
    public string Reason { get; set; } = string.Empty;
}

/// <summary>MAT-06 (010-R13): el organizador fija el ganador.</summary>
public sealed class ResolveMatchRequest
{
    [Required]
    public string WinnerGroupId { get; set; } = string.Empty;

    [StringLength(140)]
    public string? Reason { get; set; }
}

/// <summary>TOR-09 (010-R8): grupos a invitar.</summary>
public sealed class InviteGroupsRequest
{
    [Required, MinLength(1), MaxLength(16)]
    public List<string> GroupIds { get; set; } = [];
}

public sealed class InviteSkippedResponse
{
    public string GroupId { get; init; } = string.Empty;
    /// <summary>not_member (el organizador no es miembro activo), already_joined o not_found.</summary>
    public string Reason { get; init; } = string.Empty;
}

public sealed class InviteGroupsResponse
{
    public IReadOnlyList<string> Invited { get; init; } = [];
    public IReadOnlyList<InviteSkippedResponse> Skipped { get; init; } = [];
    public string Code { get; init; } = string.Empty;
}
