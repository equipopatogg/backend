namespace Pactogg.Api.Modules.Points.Dtos;

public sealed class PointsMovementResponse
{
    public string Id { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public int Amount { get; init; }
    public int BalanceAfter { get; init; }
    public string RefType { get; init; } = string.Empty;
    public string RefId { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}

public sealed class MyPointsResponse
{
    public string GroupId { get; init; } = string.Empty;
    public int Balance { get; init; }
    public IReadOnlyList<PointsMovementResponse> Movements { get; init; } = [];
}

public sealed class RankingEntryResponse
{
    public int Position { get; init; }
    public string UserId { get; init; } = string.Empty;
    public string Nickname { get; init; } = string.Empty;
    public string AvatarId { get; init; } = "default";
    public int PointsBalance { get; init; }
}

/// <summary>ACH-03. Rey del Grupo = primer lugar sin empate; null si hay empate en el primer lugar.</summary>
public sealed class GroupRankingResponse
{
    public RankingEntryResponse? King { get; init; }
    public IReadOnlyList<RankingEntryResponse> Items { get; init; } = [];
}
