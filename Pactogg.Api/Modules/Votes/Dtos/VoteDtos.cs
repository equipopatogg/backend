using System.ComponentModel.DataAnnotations;
using Pactogg.Api.Modules.Contracts.Dtos;

namespace Pactogg.Api.Modules.Votes.Dtos;

public sealed class CastVoteRequest
{
    [Required, RegularExpression("^(legit|cheat)$", ErrorMessage = "vote debe ser legit o cheat.")]
    public string Vote { get; set; } = string.Empty;

    [StringLength(140)]
    public string? Comment { get; set; }
}

public sealed class ResolveVotingRequest
{
    [Required, RegularExpression("^(fulfilled|cheating)$", ErrorMessage = "outcome debe ser fulfilled o cheating.")]
    public string Outcome { get; set; } = string.Empty;
}

public sealed class CastVoteResponse
{
    public string ContractId { get; init; } = string.Empty;
    public string Vote { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public int VotesCast { get; init; }
    public int JurorsAssigned { get; init; }
    /// <summary>true si se votó todo sin consenso y ahora decide el administrador.</summary>
    public bool NeedsAdmin { get; init; }
}

public sealed class VoteSummaryResponse
{
    public string ContractId { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public int JurorsAssigned { get; init; }
    public int VotesCast { get; init; }
    public int VotesRemaining { get; init; }
    public DateTime? VotingEndsAt { get; init; }
    public bool IAmJuror { get; init; }
    public bool IVoted { get; init; }
    public bool AdminCanResolve { get; init; }
    public bool VotingExtended { get; init; }
    public DateTime? AdminDeadlineAt { get; init; }
    /// <summary>Solo cuando la votación ya cerró.</summary>
    public int? LegitVotes { get; init; }
    public int? CheatVotes { get; init; }
}

/// <summary>Voto revelado al cerrar. Anónimo (CLARIFICACION #84): no incluye quién lo emitió.</summary>
public sealed class RevealedVoteResponse
{
    public string Vote { get; init; } = string.Empty;
    public string? Comment { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed class ContractResultResponse
{
    public string ContractId { get; init; } = string.Empty;
    public string Outcome { get; init; } = string.Empty;
    public string? ResolvedBy { get; init; }
    public DateTime? ResolvedAt { get; init; }
    public int LegitVotes { get; init; }
    public int CheatVotes { get; init; }
    public int BondRequired { get; init; }
    /// <summary>bond_release (la fianza regresó a quien cumplió), bond_forfeit (pasó al retador) o none.</summary>
    public string BondMovement { get; init; } = "none";
    public string ChallengerId { get; init; } = string.Empty;
    public string? ExecutorId { get; init; }
    public string Reward { get; init; } = string.Empty;
    public IReadOnlyList<RevealedVoteResponse> Votes { get; init; } = [];
}

public sealed class PendingVoteResponse
{
    public ContractResponse Contract { get; init; } = new();
    public DateTime? VotingEndsAt { get; init; }
}
