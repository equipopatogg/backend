using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Pactogg.Api.Modules.Auth.Domain;

[BsonIgnoreExtraElements]
public sealed class RefreshToken
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("user_id")]
    public ObjectId UserId { get; set; }

    [BsonElement("token_hash")]
    public string TokenHash { get; set; } = string.Empty;

    [BsonElement("device_id")]
    public string? DeviceId { get; set; }

    [BsonElement("expires_at")]
    public DateTime ExpiresAt { get; set; }

    [BsonElement("revoked_at")]
    public DateTime? RevokedAt { get; set; }

    /// <summary>true si se revocó al rotarlo en /auth/refresh. Solo un token rotado que reaparece indica robo (CLAR #10).</summary>
    [BsonElement("rotated")]
    public bool Rotated { get; set; }

    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; }
}
