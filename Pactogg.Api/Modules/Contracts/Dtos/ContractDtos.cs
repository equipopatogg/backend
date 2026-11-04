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
    public int Version { get; init; }
    public DateTime CreatedAt { get; init; }
}
