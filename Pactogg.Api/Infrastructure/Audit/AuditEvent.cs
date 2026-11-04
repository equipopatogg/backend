using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Pactogg.Api.Infrastructure.Audit;

[BsonIgnoreExtraElements]
public sealed class AuditEvent
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("action")]
    public string Action { get; set; } = string.Empty;

    [BsonElement("actor_id")]
    public ObjectId? ActorId { get; set; }

    [BsonElement("entity_type")]
    public string EntityType { get; set; } = string.Empty;

    [BsonElement("entity_id")]
    public ObjectId? EntityId { get; set; }

    [BsonElement("group_id")]
    public ObjectId? GroupId { get; set; }

    [BsonElement("payload")]
    public string Payload { get; set; } = string.Empty;

    [BsonElement("prev_hash")]
    public string PrevHash { get; set; } = string.Empty;

    [BsonElement("hash")]
    public string Hash { get; set; } = string.Empty;

    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; }
}
