using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Pactogg.Api.Modules.Groups.Domain;

[BsonIgnoreExtraElements]
public sealed class Group
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("owner_id")]
    public ObjectId OwnerId { get; set; }

    [BsonElement("invite_code")]
    public string InviteCode { get; set; } = string.Empty;

    [BsonElement("invite_active")]
    public bool InviteActive { get; set; } = true;

    [BsonElement("settings")]
    public GroupSettings Settings { get; set; } = new();

    [BsonElement("member_count")]
    public int MemberCount { get; set; }

    [BsonElement("current_season")]
    public int CurrentSeason { get; set; } = 1;

    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [BsonElement("deleted_at")]
    public DateTime? DeletedAt { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class GroupSettings
{
    [BsonElement("jurors")]
    public int Jurors { get; set; } = 3;

    [BsonElement("consensus")]
    public double Consensus { get; set; } = 0.66;

    [BsonElement("bond_options")]
    public List<int> BondOptions { get; set; } = [25, 50, 100];
}
