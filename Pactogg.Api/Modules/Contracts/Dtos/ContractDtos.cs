using System.ComponentModel.DataAnnotations;

namespace Pactogg.Api.Modules.Contracts.Dtos;

public sealed class CreateContractRequest
{
    public string GroupId { get; set; } = string.Empty;

    [Required, StringLength(60, MinimumLength = 1)]
    public string Game { get; set; } = string.Empty;

    [Required, StringLength(280, MinimumLength = 1)]
    public string Condition { get; set; } = string.Empty;

    [Required, StringLength(80, MinimumLength = 3)]
    public string Reward { get; set; } = string.Empty;

    [Required]
    public DateTimeOffset Deadline { get; set; }

    [Required]
    public int BondRequired { get; set; }

    public string TargetType { get; set; } = "group";

    public string? TargetId { get; set; }

    public string Source { get; set; } = "manual";
}

public sealed class ContractResponse
{
    public string Id { get; init; } = string.Empty;
    public string GroupId { get; init; } = string.Empty;
    public string ChallengerId { get; init; } = string.Empty;
    public string TargetType { get; init; } = string.Empty;
    public string? TargetId { get; init; }
    public string Game { get; init; } = string.Empty;
    public string Condition { get; init; } = string.Empty;
    public string Reward { get; init; } = string.Empty;
    public DateTime Deadline { get; init; }
    public int BondRequired { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? AcceptedBy { get; init; }
    public DateTime? AcceptedAt { get; init; }
    public string? ClosedReason { get; init; }
    public string? ExecutorId { get; init; }
    public int JurorsCount { get; init; }
    public DateTime? VotingEndsAt { get; init; }
    public string? ResolvedBy { get; init; }
    public DateTime? ResolvedAt { get; init; }
    public int EvidenceCount { get; init; }
    public int Version { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }

    /// <summary>Solo en la respuesta de aceptar: saldo del usuario en el grupo después de retener la fianza.</summary>
    public int? MyPoints { get; init; }
}

public sealed class ContractActionRequest
{
    /// <summary>Versión que la app vio; si otro la cambió antes, 409. Opcional.</summary>
    public int? Version { get; set; }
}
