using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Pactogg.Api.Modules.Tournaments.Domain;

[BsonIgnoreExtraElements]
public sealed class Tournament
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("group_id")]
    public ObjectId GroupId { get; set; }

    [BsonElement("organizer_id")]
    public ObjectId OrganizerId { get; set; }

    [BsonElement("game")]
    public string Game { get; set; } = string.Empty;

    [BsonElement("format")]
    public string Format { get; set; } = "single_elimination";

    [BsonElement("entry_points")]
    public int EntryPoints { get; set; }

    [BsonElement("prize_pool")]
    public int PrizePool { get; set; }

    [BsonElement("scheduled_at")]
    public DateTime ScheduledAt { get; set; }

    [BsonElement("participants")]
    public List<ObjectId> Participants { get; set; } = [];

    [BsonElement("status")]
    public string Status { get; set; } = "open";

    [BsonElement("winner_group_id")]
    public ObjectId? WinnerGroupId { get; set; }

    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("updated_at")]
    public DateTime UpdatedAt { get; set; }
}
