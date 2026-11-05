using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Pactogg.Api.Modules.Contracts.Domain;

[BsonIgnoreExtraElements]
public sealed class Contract
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("group_id")]
    public ObjectId GroupId { get; set; }

    [BsonElement("challenger_id")]
    public ObjectId ChallengerId { get; set; }

    [BsonElement("target_type")]
    public string TargetType { get; set; } = "group";

    [BsonElement("target_id")]
    public ObjectId? TargetId { get; set; }

    [BsonElement("game")]
    public string Game { get; set; } = string.Empty;

    [BsonElement("condition")]
    public string Condition { get; set; } = string.Empty;

    [BsonElement("reward")]
    public string Reward { get; set; } = string.Empty;

    [BsonElement("deadline")]
    public DateTime Deadline { get; set; }

    [BsonElement("bond_required")]
    public int BondRequired { get; set; }

    [BsonElement("status")]
    public string Status { get; set; } = "open";

    [BsonElement("accepted_by")]
    public ObjectId? AcceptedBy { get; set; }

    [BsonElement("accepted_at")]
    public DateTime? AcceptedAt { get; set; }

    [BsonElement("rejected_by")]
    public ObjectId? RejectedBy { get; set; }

    [BsonElement("closed_reason")]
    public string? ClosedReason { get; set; }

    /// <summary>Quien debe cumplir el reto: accepted_by, o el retado, o quien subió la primera evidencia.</summary>
    [BsonElement("executor_id")]
    public ObjectId? ExecutorId { get; set; }

    [BsonElement("jurors")]
    public List<ObjectId> Jurors { get; set; } = [];

    [BsonElement("voters")]
    public List<ObjectId> Voters { get; set; } = [];

    [BsonElement("legit_votes")]
    public int LegitVotes { get; set; }

    [BsonElement("cheat_votes")]
    public int CheatVotes { get; set; }

    [BsonElement("voting_started_at")]
    public DateTime? VotingStartedAt { get; set; }

    [BsonElement("voting_ends_at")]
    public DateTime? VotingEndsAt { get; set; }

    /// <summary>jury o admin.</summary>
    [BsonElement("resolved_by")]
    public string? ResolvedBy { get; set; }

    [BsonElement("resolved_at")]
    public DateTime? ResolvedAt { get; set; }

    [BsonElement("source")]
    public string Source { get; set; } = "manual";

    [BsonElement("original_phrase")]
    public string? OriginalPhrase { get; set; }

    [BsonElement("evidence_count")]
    public int EvidenceCount { get; set; }

    [BsonElement("version")]
    public int Version { get; set; } = 1;

    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("updated_at")]
    public DateTime UpdatedAt { get; set; }
}
