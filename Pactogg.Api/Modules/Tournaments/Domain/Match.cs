using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Pactogg.Api.Modules.Tournaments.Domain;

[BsonIgnoreExtraElements]
public sealed class Match
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("tournament_id")]
    public ObjectId TournamentId { get; set; }

    [BsonElement("round")]
    public int Round { get; set; }

    [BsonElement("position")]
    public int Position { get; set; }

    [BsonElement("p1_group_id")]
    public ObjectId? P1GroupId { get; set; }

    [BsonElement("p2_group_id")]
    public ObjectId? P2GroupId { get; set; }

    [BsonElement("p1_score")]
    public int? P1Score { get; set; }

    [BsonElement("p2_score")]
    public int? P2Score { get; set; }

    [BsonElement("winner_group_id")]
    public ObjectId? WinnerGroupId { get; set; }

    /// <summary>pending, reported, confirmed, disputed, bye o cancelled.</summary>
    [BsonElement("status")]
    public string Status { get; set; } = "pending";

    /// <summary>Representantes que jugaron por cada grupo (010-D4): quien reporta, confirma o disputa.</summary>
    [BsonElement("p1_representatives")]
    public List<ObjectId> P1Representatives { get; set; } = [];

    [BsonElement("p2_representatives")]
    public List<ObjectId> P2Representatives { get; set; } = [];

    [BsonElement("reported_at")]
    public DateTime? ReportedAt { get; set; }

    [BsonElement("dispute_reason")]
    public string? DisputeReason { get; set; }

    [BsonElement("resolved_by")]
    public ObjectId? ResolvedBy { get; set; }

    [BsonElement("screenshot_key")]
    public string? ScreenshotKey { get; set; }

    [BsonElement("reported_by")]
    public ObjectId? ReportedBy { get; set; }

    [BsonElement("next_match_id")]
    public ObjectId? NextMatchId { get; set; }

    [BsonElement("updated_at")]
    public DateTime UpdatedAt { get; set; }
}
