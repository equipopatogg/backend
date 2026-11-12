using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Pactogg.Api.Modules.Tournaments.Domain;

[BsonIgnoreExtraElements]
public sealed class Tournament
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("group_id")]
    public ObjectId GroupId { get; set; }

    [BsonElement("organizer_id")]
    public ObjectId OrganizerId { get; set; }

    /// <summary>Código de 6 caracteres para que otros grupos se inscriban (010-D3).</summary>
    [BsonElement("code")]
    public string? Code { get; set; }

    /// <summary>Tamaño máximo: 4, 8 o 16 grupos (010-R1).</summary>
    [BsonElement("max_groups")]
    public int MaxGroups { get; set; } = 8;

    /// <summary>Inscripciones con quién pagó, para devolverlas (010-D2).</summary>
    [BsonElement("entries")]
    public List<TournamentEntry> Entries { get; set; } = [];

    [BsonElement("started_at")]
    public DateTime? StartedAt { get; set; }

    [BsonElement("finished_at")]
    public DateTime? FinishedAt { get; set; }

    [BsonElement("cancelled_at")]
    public DateTime? CancelledAt { get; set; }

    /// <summary>by_organizer o not_enough_groups (010-R16).</summary>
    [BsonElement("cancel_reason")]
    public string? CancelReason { get; set; }

    [BsonElement("game")]
    public string Game { get; set; } = string.Empty;

    [BsonElement("format")]
    public string Format { get; set; } = "single_elimination";

    [BsonElement("entry_points")]
    public int EntryPoints { get; set; }

    [BsonElement("prize_pool")]
    public int PrizePool { get; set; }

    [BsonElement("scheduled_at")]
    public DateTime ScheduledAt { get; set; }

    [BsonElement("participants")]
    public List<ObjectId> Participants { get; set; } = [];

    [BsonElement("status")]
    public string Status { get; set; } = "open";

    [BsonElement("winner_group_id")]
    public ObjectId? WinnerGroupId { get; set; }

    [BsonElement("created_at")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("updated_at")]
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Inscripción de un grupo: la paga el administrador que inscribe, de su saldo en ese grupo.</summary>
public sealed class TournamentEntry
{
    [BsonElement("group_id")]
    public ObjectId GroupId { get; set; }

    [BsonElement("paid_by")]
    public ObjectId PaidBy { get; set; }

    [BsonElement("amount")]
    public int Amount { get; set; }

    /// <summary>active o refunded.</summary>
    [BsonElement("status")]
    public string Status { get; set; } = "active";

    [BsonElement("joined_at")]
    public DateTime JoinedAt { get; set; }
}
