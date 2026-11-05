namespace Pactogg.Api.Modules.Commitments.Dtos;

public sealed class CommitmentPartyResponse
{
    public string UserId { get; init; } = string.Empty;
    public string Nickname { get; init; } = string.Empty;
}

public sealed class CommitmentResponse
{
    public string Id { get; init; } = string.Empty;
    public string GroupId { get; init; } = string.Empty;
    public string ContractId { get; init; } = string.Empty;
    public CommitmentPartyResponse Debtor { get; init; } = new();
    public CommitmentPartyResponse Creditor { get; init; } = new();
    public string Description { get; init; } = string.Empty;
    public string Game { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime? RequestedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public DateTime CreatedAt { get; init; }
}
