using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Pactogg.Api.Modules.Notifications.Domain;

/// <summary>Aviso dentro de la app (colección notifications).</summary>
[BsonIgnoreExtraElements]
public sealed class Notification
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("user_id")]
    public ObjectId UserId { get; set; }

    /// <summary>contract_received, contract_accepted, vote_requested, contract_resolved, commitment_completion_requested, commitment_completed.</summary>
    [BsonElement("type")]
    public string Type { get; set; } = string.Empty;

    [BsonElement("title")]
    public string Title { get; set; } = string.Empty;

    [BsonElement("body")]
    public string Body { get; set; } = string.Empty;

    [BsonElement("group_id")]
    public ObjectId? GroupId { get; set; }

    [BsonElement("contract_id")]
    public ObjectId? ContractId { get; set; }

    [BsonElement("commitment_id")]
    public ObjectId? CommitmentId { get; set; }

    [BsonElement("read_at")]
    public DateTime? ReadAt { get; set; }

    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; }
}
