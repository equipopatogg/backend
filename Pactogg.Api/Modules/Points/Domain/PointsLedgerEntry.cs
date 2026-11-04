using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Pactogg.Api.Modules.Points.Domain;

/// <summary>Movimiento inmutable del libro de puntos (points_ledger). Solo inserción.</summary>
[BsonIgnoreExtraElements]
public sealed class PointsLedgerEntry
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("group_id")]
    public ObjectId GroupId { get; set; }

    [BsonElement("user_id")]
    public ObjectId UserId { get; set; }

    /// <summary>bond_lock, bond_release, bond_forfeit, reward, tournament_entry, tournament_prize o adjustment.</summary>
    [BsonElement("type")]
    public string Type { get; set; } = string.Empty;

    [BsonElement("amount")]
    public int Amount { get; set; }

    [BsonElement("ref_type")]
    public string RefType { get; set; } = string.Empty;

    [BsonElement("ref_id")]
    public ObjectId RefId { get; set; }

    [BsonElement("balance_after")]
    public int BalanceAfter { get; set; }

    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; }
}
