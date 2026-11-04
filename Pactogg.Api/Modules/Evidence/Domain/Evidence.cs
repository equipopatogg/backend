using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Pactogg.Api.Modules.Evidence.Domain;

[BsonIgnoreExtraElements]
public sealed class Evidence
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("contract_id")]
    public ObjectId ContractId { get; set; }

    [BsonElement("user_id")]
    public ObjectId UserId { get; set; }

    [BsonElement("type")]
    public string Type { get; set; } = "link";

    [BsonElement("object_key")]
    public string? ObjectKey { get; set; }

    [BsonElement("url")]
    public string? Url { get; set; }

    [BsonElement("provider")]
    public string? Provider { get; set; }

    [BsonElement("size_bytes")]
    public long? SizeBytes { get; set; }

    [BsonElement("moderation")]
    public ModerationRecord Moderation { get; set; } = new();

    [BsonElement("saved_in_fame")]
    public bool SavedInFame { get; set; }

    [BsonElement("expires_at")]
    public DateTime? ExpiresAt { get; set; }

    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class ModerationRecord
{
    [BsonElement("status")]
    public string Status { get; set; } = "pending";

    [BsonElement("labels")]
    public List<string> Labels { get; set; } = [];

    [BsonElement("moderated_at")]
    public DateTime? ModeratedAt { get; set; }
}
