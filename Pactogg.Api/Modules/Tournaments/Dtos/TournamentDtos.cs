namespace Pactogg.Api.Modules.Tournaments.Dtos;

public sealed class MatchResponse
{
    public string Id { get; init; } = string.Empty;
    public int Round { get; init; }
    public int Position { get; init; }
    public string? P1GroupId { get; init; }
    public string? P2GroupId { get; init; }
    public int? P1Score { get; init; }
    public int? P2Score { get; init; }
    public string? WinnerGroupId { get; init; }
    public string Status { get; init; } = string.Empty;
}

public sealed class BracketResponse
{
    public IReadOnlyList<MatchResponse> Matches { get; init; } = [];
}

public sealed class TournamentResponse
{
    public string Id { get; init; } = string.Empty;
    public string GroupId { get; init; } = string.Empty;
    public string Game { get; init; } = string.Empty;
    public string Format { get; init; } = string.Empty;
    public int EntryPoints { get; init; }
    public int PrizePool { get; init; }
    public DateTime ScheduledAt { get; init; }
    public IReadOnlyList<string> Participants { get; init; } = [];
    public string Status { get; init; } = string.Empty;
    public string? WinnerGroupId { get; init; }
    public BracketResponse Bracket { get; init; } = new();
}
