using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Pactogg.Api.Modules.Auth.Domain;

[BsonIgnoreExtraElements]
public sealed class User
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("email")]
    public string Email { get; set; } = string.Empty;

    [BsonElement("password_hash")]
    public string PasswordHash { get; set; } = string.Empty;

    [BsonElement("nickname")]
    public string Nickname { get; set; } = string.Empty;

    [BsonElement("nickname_lower")]
    public string NicknameLower { get; set; } = string.Empty;

    [BsonElement("avatar_id")]
    public string AvatarId { get; set; } = "default";

    [BsonElement("email_verified")]
    public bool EmailVerified { get; set; }

    [BsonElement("stats")]
    public UserStats Stats { get; set; } = new();

    [BsonElement("consent")]
    public ConsentRecord Consent { get; set; } = new();

    [BsonElement("status")]
    public string Status { get; set; } = "active";

    [BsonElement("last_login_at")]
    public DateTime? LastLoginAt { get; set; }

    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [BsonElement("deleted_at")]
    public DateTime? DeletedAt { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class UserStats
{
    [BsonElement("completed")]
    public int Completed { get; set; }

    [BsonElement("cheated")]
    public int Cheated { get; set; }

    [BsonElement("streak")]
    public int Streak { get; set; }

    [BsonElement("trophies")]
    public int Trophies { get; set; }

    [BsonElement("word_index")]
    public int WordIndex { get; set; } = 50;
}

[BsonIgnoreExtraElements]
public sealed class ConsentRecord
{
    [BsonElement("version")]
    public string Version { get; set; } = "2026-10-01";

    [BsonElement("accepted_at")]
    public DateTime AcceptedAt { get; set; }
}
