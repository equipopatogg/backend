using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Pactogg.Api.Modules.Votes.Domain;

/// <summary>Voto inmutable de un jurado (colección votes, único por contract_id + juror_id).</summary>
[BsonIgnoreExtraElements]
public sealed class Vote
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("contract_id")]
    public ObjectId ContractId { get; set; }

    [BsonElement("group_id")]
    public ObjectId GroupId { get; set; }

    [BsonElement("juror_id")]
    public ObjectId JurorId { get; set; }

    /// <summary>legit o cheat.</summary>
    [BsonElement("vote")]
    public string Value { get; set; } = string.Empty;

    [BsonElement("comment")]
    public string? Comment { get; set; }

    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; }
}
