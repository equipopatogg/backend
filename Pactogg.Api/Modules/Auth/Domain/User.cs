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

    /// <summary>"sub" de la cuenta de Google vinculada (ID 12). Null si solo usa correo y contraseña.</summary>
    [BsonElement("google_sub")]
    public string? GoogleSub { get; set; }

    [BsonElement("avatar_id")]
    public string AvatarId { get; set; } = "default";

    [BsonElement("email_verified")]
    public bool EmailVerified { get; set; }

    [BsonElement("stats")]
    public UserStats Stats { get; set; } = new();

    [BsonElement("consent")]
    public ConsentRecord Consent { get; set; } = new();

    /// <summary>DEV-03: qué tipos de push quiere recibir (CLARIFICACION #13).</summary>
    [BsonElement("notification_preferences")]
    public NotificationPreferences NotificationPreferences { get; set; } = new();

    [BsonElement("password_changed_at")]
    public DateTime? PasswordChangedAt { get; set; }

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

    // Contadores del Índice de Palabra (spec 008-R12). El índice se recalcula solo desde estos números.

    /// <summary>Retos aceptados que vencieron sin evidencia.</summary>
    [BsonElement("expired")]
    public int Expired { get; set; }

    /// <summary>Compromisos que la persona cumplió dentro de los 7 días.</summary>
    [BsonElement("commitments_on_time")]
    public int CommitmentsOnTime { get; set; }

    /// <summary>Compromisos que la persona dejó pendientes más de 7 días.</summary>
    [BsonElement("commitments_overdue")]
    public int CommitmentsOverdue { get; set; }

    /// <summary>Votos como jurado que coincidieron con la decisión del jurado.</summary>
    [BsonElement("aligned_votes")]
    public int AlignedVotes { get; set; }
}

/// <summary>Cambios a los contadores del Índice de Palabra.</summary>
public sealed record StatsDelta(int Expired = 0, int CommitmentsOnTime = 0, int CommitmentsOverdue = 0, int AlignedVotes = 0)
{
    public static readonly StatsDelta None = new();
}

[BsonIgnoreExtraElements]
public sealed class ConsentRecord
{
    [BsonElement("version")]
    public string Version { get; set; } = "2026-10-01";

    [BsonElement("accepted_at")]
    public DateTime AcceptedAt { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class NotificationPreferences
{
    [BsonElement("retos")]
    public bool Retos { get; set; } = true;

    [BsonElement("votos")]
    public bool Votos { get; set; } = true;

    [BsonElement("torneos")]
    public bool Torneos { get; set; } = true;

    [BsonElement("compromisos")]
    public bool Compromisos { get; set; } = true;
}
