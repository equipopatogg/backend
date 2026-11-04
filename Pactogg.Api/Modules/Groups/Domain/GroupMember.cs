using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Pactogg.Api.Modules.Groups.Domain;

[BsonIgnoreExtraElements]
public sealed class GroupMember
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("group_id")]
    public ObjectId GroupId { get; set; }

    [BsonElement("user_id")]
    public ObjectId UserId { get; set; }

    [BsonElement("role")]
    public string Role { get; set; } = "member";

    [BsonElement("nick_lower")]
    public string NickLower { get; set; } = string.Empty;

    [BsonElement("points_balance")]
    public int PointsBalance { get; set; }

    [BsonElement("status")]
    public string Status { get; set; } = "active";

    [BsonElement("muted")]
    public bool Muted { get; set; }

    [BsonElement("joined_at")]
    public DateTime JoinedAt { get; set; }

    [BsonElement("updated_at")]
    public DateTime UpdatedAt { get; set; }
}
