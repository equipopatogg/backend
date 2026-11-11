using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Pactogg.Api.Modules.Commitments.Domain;

/// <summary>Lo que alguien debe cumplir en la vida real al cumplirse un reto. Sin montos de dinero.</summary>
[BsonIgnoreExtraElements]
public sealed class Commitment
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("group_id")]
    public ObjectId GroupId { get; set; }

    [BsonElement("contract_id")]
    public ObjectId ContractId { get; set; }

    /// <summary>Quien debe (el retador que prometió la recompensa).</summary>
    [BsonElement("debtor_id")]
    public ObjectId DebtorId { get; set; }

    /// <summary>A quien se le debe (quien cumplió el reto).</summary>
    [BsonElement("creditor_id")]
    public ObjectId CreditorId { get; set; }

    [BsonElement("description")]
    public string Description { get; set; } = string.Empty;

    [BsonElement("game")]
    public string Game { get; set; } = string.Empty;

    /// <summary>pending, completion_requested o completed.</summary>
    [BsonElement("status")]
    public string Status { get; set; } = "pending";

    [BsonElement("requested_at")]
    public DateTime? RequestedAt { get; set; }

    [BsonElement("completed_at")]
    public DateTime? CompletedAt { get; set; }

    /// <summary>Spec 009-R4: cuándo se avisó al deudor que el compromiso lleva más de 7 días pendiente (una sola vez).</summary>
    [BsonElement("overdue_at")]
    public DateTime? OverdueAt { get; set; }

    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("updated_at")]
    public DateTime UpdatedAt { get; set; }
}
