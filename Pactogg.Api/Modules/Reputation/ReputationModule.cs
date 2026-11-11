using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Pactogg.Api.Common;
using Pactogg.Api.Common.Extensions;
using Pactogg.Api.Infrastructure.Redis;
using Pactogg.Api.Modules.Auth.Abstractions;
using Pactogg.Api.Modules.Auth.Domain;
using Pactogg.Api.Modules.Commitments.Domain;
using Pactogg.Api.Modules.Contracts.Abstractions;
using Pactogg.Api.Modules.Contracts.Domain;
using Pactogg.Api.Modules.Groups;
using Pactogg.Api.Modules.Groups.Abstractions;
using Pactogg.Api.Modules.Votes.Abstractions;

namespace Pactogg.Api.Modules.Reputation;

/// <summary>
/// Spec 008-R12 y 008-D2 (constitución §6.8): Índice de Palabra determinista calculado solo desde contadores.
/// índice = clamp(0, 100, 50 + 4·cumplidos − 12·trampas − 5·vencidos + 2·compromisos_a_tiempo − 4·compromisos_vencidos + min(10, votos_alineados))
/// </summary>
public static class WordIndexCalculator
{
    public const int Base = 50;

    public static int Compute(UserStats stats) =>
        Compute(stats.Completed, stats.Cheated, stats.Expired, stats.CommitmentsOnTime, stats.CommitmentsOverdue, stats.AlignedVotes);

    public static int Compute(int completed, int cheated, int expired, int commitmentsOnTime, int commitmentsOverdue, int alignedVotes)
    {
        long value = Base
            + 4L * completed
            - 12L * cheated
            - 5L * expired
            + 2L * commitmentsOnTime
            - 4L * commitmentsOverdue
            + Math.Min(10, Math.Max(0, alignedVotes));
        return (int)Math.Clamp(value, 0, 100);
    }
}

/// <summary>Logro ganado (colección achievements). Único por persona, código y fuente (source_key).</summary>
[BsonIgnoreExtraElements]
public sealed class Achievement
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("user_id")]
    public ObjectId UserId { get; set; }

    [BsonElement("group_id")]
    public ObjectId? GroupId { get; set; }

    /// <summary>badge, streak o trophy (tipos del CRUD).</summary>
    [BsonElement("type")]
    public string Type { get; set; } = "badge";

    [BsonElement("code")]
    public string Code { get; set; } = string.Empty;

    [BsonElement("source_type")]
    public string SourceType { get; set; } = "contract";

    [BsonElement("source_id")]
    public ObjectId SourceId { get; set; }

    /// <summary>Vacío para los logros que se ganan una sola vez; el id del torneo para el trofeo.</summary>
    [BsonElement("source_key")]
    public string SourceKey { get; set; } = string.Empty;

    [BsonElement("earned_at")]
    public DateTime EarnedAt { get; set; }
}

public sealed record AchievementDefinition(string Code, string Type, string Name, string Description, bool PerSource);

/// <summary>Spec 008-D1: catálogo v1. Más logros se agregan por enmienda de la spec 008.</summary>
public static class AchievementCatalog
{
    public static readonly AchievementDefinition PrimerReto = new("primer_reto", "badge", "Primer reto", "Cumpliste tu primer reto.", false);
    public static readonly AchievementDefinition Racha3 = new("racha_3", "streak", "Racha de 3", "3 retos cumplidos seguidos.", false);
    public static readonly AchievementDefinition Racha5 = new("racha_5", "streak", "Racha de 5", "5 retos cumplidos seguidos.", false);
    public static readonly AchievementDefinition Racha10 = new("racha_10", "streak", "Racha de 10", "10 retos cumplidos seguidos.", false);
    public static readonly AchievementDefinition CampeonTorneo = new("campeon_torneo", "trophy", "Campeón de torneo", "Tu grupo ganó un torneo.", true);

    public static readonly IReadOnlyDictionary<string, AchievementDefinition> All =
        new[] { PrimerReto, Racha3, Racha5, Racha10, CampeonTorneo }.ToDictionary(x => x.Code);

    public static readonly IReadOnlyList<(int Streak, AchievementDefinition Definition)> Streaks =
        [(3, Racha3), (5, Racha5), (10, Racha10)];
}

public interface IAchievementRepository
{
    /// <summary>Inserta el logro; false si la persona ya lo tenía para esa fuente (índice único).</summary>
    Task<bool> TryAwardAsync(Achievement achievement, CancellationToken cancellationToken);

    Task<IReadOnlyList<Achievement>> ListByUserAsync(ObjectId userId, CancellationToken cancellationToken);
}

public sealed class AchievementResponse
{
    public string Id { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string SourceType { get; init; } = string.Empty;
    public string SourceId { get; init; } = string.Empty;
    public string? GroupId { get; init; }
    public DateTime EarnedAt { get; init; }
}

public sealed class ShameExecutorResponse
{
    public string UserId { get; init; } = string.Empty;
    public string Nickname { get; init; } = string.Empty;
    public string AvatarId { get; init; } = "default";
}

/// <summary>ACH-02: un reto del Muro de la Vergüenza (sin fotos ni comentarios).</summary>
public sealed class ShameItemResponse
{
    public string ContractId { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public ShameExecutorResponse? Executor { get; init; }
    public string Game { get; init; } = string.Empty;
    public string Condition { get; init; } = string.Empty;
    public DateTime? ClosedAt { get; init; }
}

/// <summary>
/// Reputación: Índice de Palabra (008-T1), logros (008-T3) y Muro de la Vergüenza (008-T2).
/// Los ganchos se llaman después de cada cierre; todos son idempotentes respecto a la fuente salvo los contadores,
/// que se llaman una sola vez por transición porque la transición misma es condicional.
/// </summary>
public sealed class ReputationService
{
    public static readonly TimeSpan CommitmentGrace = TimeSpan.FromDays(7);

    private readonly IUserRepository _users;
    private readonly IVoteRepository _votes;
    private readonly IAchievementRepository _achievements;
    private readonly IContractRepository _contracts;
    private readonly IGroupMemberRepository _members;
    private readonly GroupAccess _access;
    private readonly ICacheService _cache;

    public ReputationService(
        IUserRepository users,
        IVoteRepository votes,
        IAchievementRepository achievements,
        IContractRepository contracts,
        IGroupMemberRepository members,
        GroupAccess access,
        ICacheService cache)
    {
        _users = users;
        _votes = votes;
        _achievements = achievements;
        _contracts = contracts;
        _members = members;
        _access = access;
        _cache = cache;
    }

    /// <summary>Reto cerrado por el jurado o el administrador: recalcula al ejecutor, cuenta votos alineados y da logros.</summary>
    public async Task OnContractResolvedAsync(Contract contract, string resolvedBy, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var fulfilled = contract.Status == "fulfilled";
        var executor = contract.ExecutorId ?? contract.AcceptedBy ?? contract.TargetId;
        if (executor is ObjectId executorId)
        {
            var stats = await _users.ApplyStatsAsync(executorId, StatsDelta.None, now, cancellationToken);
            if (fulfilled && stats is not null && executorId != contract.ChallengerId)
            {
                if (stats.Completed >= 1)
                {
                    await AwardAsync(executorId, AchievementCatalog.PrimerReto, contract, now, cancellationToken);
                }

                foreach (var (streak, definition) in AchievementCatalog.Streaks)
                {
                    if (stats.Streak >= streak)
                    {
                        await AwardAsync(executorId, definition, contract, now, cancellationToken);
                    }
                }
            }

            await _cache.BumpVersionAsync(CacheExtensions.UserScope(executorId), cancellationToken);
        }

        if (resolvedBy == "jury")
        {
            var expected = fulfilled ? "legit" : "cheat";
            foreach (var vote in await _votes.ListByContractAsync(contract.Id, cancellationToken))
            {
                if (vote.Value == expected)
                {
                    await _users.ApplyStatsAsync(vote.JurorId, new StatsDelta(AlignedVotes: 1), now, cancellationToken);
                    await _cache.BumpVersionAsync(CacheExtensions.UserScope(vote.JurorId), cancellationToken);
                }
            }
        }
    }

    /// <summary>Reto aceptado que venció sin evidencia: cuenta como vencido para quien lo aceptó.</summary>
    public async Task OnContractExpiredAsync(Contract contract, CancellationToken cancellationToken)
    {
        if (contract.AcceptedBy is ObjectId acceptor)
        {
            await _users.ApplyStatsAsync(acceptor, new StatsDelta(Expired: 1), DateTime.UtcNow, cancellationToken);
            await _cache.BumpVersionAsync(CacheExtensions.UserScope(acceptor), cancellationToken);
        }
    }

    /// <summary>Compromiso confirmado: suma "a tiempo" al deudor si se cumplió dentro de los 7 días y nunca se marcó vencido.</summary>
    public async Task OnCommitmentCompletedAsync(Commitment commitment, DateTime now, CancellationToken cancellationToken)
    {
        if (commitment.OverdueAt is null && now - commitment.CreatedAt <= CommitmentGrace)
        {
            await _users.ApplyStatsAsync(commitment.DebtorId, new StatsDelta(CommitmentsOnTime: 1), now, cancellationToken);
            await _cache.BumpVersionAsync(CacheExtensions.UserScope(commitment.DebtorId), cancellationToken);
        }
    }

    /// <summary>Compromiso con más de 7 días pendiente (se marca una sola vez).</summary>
    public async Task OnCommitmentOverdueAsync(Commitment commitment, DateTime now, CancellationToken cancellationToken)
    {
        await _users.ApplyStatsAsync(commitment.DebtorId, new StatsDelta(CommitmentsOverdue: 1), now, cancellationToken);
        await _cache.BumpVersionAsync(CacheExtensions.UserScope(commitment.DebtorId), cancellationToken);
    }

    /// <summary>Trofeo campeon_torneo a cada miembro que jugó (se llama al cerrar un torneo, spec 010).</summary>
    public Task AwardTournamentChampionAsync(ObjectId userId, ObjectId groupId, ObjectId tournamentId, DateTime now, CancellationToken cancellationToken) =>
        _achievements.TryAwardAsync(new Achievement
        {
            Id = ObjectId.GenerateNewId(),
            UserId = userId,
            GroupId = groupId,
            Type = AchievementCatalog.CampeonTorneo.Type,
            Code = AchievementCatalog.CampeonTorneo.Code,
            SourceType = "tournament",
            SourceId = tournamentId,
            SourceKey = tournamentId.ToString(),
            EarnedAt = now
        }, cancellationToken);

    /// <summary>ACH-02 (008-R9): retos del grupo que terminaron en trampa o vencidos, en tono ligero.</summary>
    public async Task<(IReadOnlyList<ShameItemResponse> Items, object Meta)> ShameAsync(
        ObjectId userId,
        string gid,
        int limit,
        string? cursor,
        CancellationToken cancellationToken)
    {
        ListQuery.ValidateLimit(limit);
        var cursorId = ListQuery.ParseCursor(cursor);
        var (group, _) = await _access.RequireMemberAsync(userId, gid, cancellationToken);
        var version = await _cache.GetVersionAsync(CacheExtensions.GroupScope(group.Id), cancellationToken);
        var snapshot = await _cache.GetOrCreateJsonAsync(
            $"shame:{group.Id}:{version}:{limit}:{cursor}",
            TimeSpan.FromSeconds(60),
            async () =>
            {
                var (items, hasMore) = await _contracts.ListShameAsync(group.Id, limit, cursorId, cancellationToken);
                var executors = items.Select(x => x.ExecutorId ?? x.AcceptedBy ?? x.TargetId).OfType<ObjectId>().Distinct().ToArray();
                var users = (await _users.ListByIdsAsync(executors, cancellationToken)).ToDictionary(x => x.Id);
                var entries = items.Select(contract =>
                {
                    var executorId = contract.ExecutorId ?? contract.AcceptedBy ?? contract.TargetId;
                    ShameExecutorResponse? executor = null;
                    if (executorId is ObjectId id && users.TryGetValue(id, out var user))
                    {
                        executor = new ShameExecutorResponse { UserId = id.ToString(), Nickname = user.Nickname, AvatarId = user.AvatarId };
                    }

                    return new ShameItemResponse
                    {
                        ContractId = contract.Id.ToString(),
                        Status = contract.Status,
                        Label = contract.Status == "cheating" ? "El jurado no se la creyó" : "Se le fue el tiempo",
                        Executor = executor,
                        Game = contract.Game,
                        Condition = contract.Condition,
                        ClosedAt = contract.ResolvedAt ?? contract.UpdatedAt
                    };
                }).ToList();
                var next = hasMore && items.Count > 0 ? items[^1].Id.ToString() : null;
                return new PageSnapshot<ShameItemResponse>(entries, next, hasMore);
            },
            cancellationToken);
        return (snapshot.Items, new { next_cursor = snapshot.NextCursor, has_more = snapshot.HasMore });
    }

    /// <summary>ACH-04 (008-R11): logros de una persona; solo para quien comparte grupo con ella (si no, 404).</summary>
    public async Task<IReadOnlyList<AchievementResponse>> ListAchievementsAsync(ObjectId viewerId, string id, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(id, out var targetId))
        {
            throw ApiException.NotFound("Usuario no encontrado.");
        }

        var target = await _users.GetByIdAsync(targetId, cancellationToken);
        if (target is null || target.Status != "active")
        {
            throw ApiException.NotFound("Usuario no encontrado.");
        }

        if (targetId != viewerId)
        {
            var mine = await _members.ListActiveGroupIdsAsync(viewerId, cancellationToken);
            var theirs = await _members.ListActiveGroupIdsAsync(targetId, cancellationToken);
            if (!mine.Intersect(theirs).Any())
            {
                throw ApiException.NotFound("Usuario no encontrado.");
            }
        }

        var version = await _cache.GetVersionAsync(CacheExtensions.UserScope(targetId), cancellationToken);
        return await _cache.GetOrCreateJsonAsync(
            $"achievements:{targetId}:{version}",
            TimeSpan.FromSeconds(60),
            async () => (await _achievements.ListByUserAsync(targetId, cancellationToken))
                .OrderByDescending(x => x.EarnedAt)
                .Select(x =>
                {
                    var definition = AchievementCatalog.All.GetValueOrDefault(x.Code);
                    return new AchievementResponse
                    {
                        Id = x.Id.ToString(),
                        Type = x.Type,
                        Code = x.Code,
                        Name = definition?.Name ?? x.Code,
                        Description = definition?.Description ?? string.Empty,
                        SourceType = x.SourceType,
                        SourceId = x.SourceId.ToString(),
                        GroupId = x.GroupId?.ToString(),
                        EarnedAt = x.EarnedAt
                    };
                })
                .ToList(),
            cancellationToken);
    }

    private async Task AwardAsync(ObjectId userId, AchievementDefinition definition, Contract source, DateTime now, CancellationToken cancellationToken)
    {
        await _achievements.TryAwardAsync(new Achievement
        {
            Id = ObjectId.GenerateNewId(),
            UserId = userId,
            GroupId = source.GroupId,
            Type = definition.Type,
            Code = definition.Code,
            SourceType = "contract",
            SourceId = source.Id,
            SourceKey = definition.PerSource ? source.Id.ToString() : string.Empty,
            EarnedAt = now
        }, cancellationToken);
    }
}

public static class ReputationEndpoints
{
    public static IEndpointRouteBuilder MapReputationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/groups/{gid}/shame", async (
                string gid,
                int? limit,
                string? cursor,
                ReputationService service,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var (items, meta) = await service.ShameAsync(context.User.GetRequiredObjectId(), gid, limit ?? 20, cursor, cancellationToken);
                return ApiResults.Ok(items, meta);
            })
            .RequireAuthorization()
            .WithTags("Achievements")
            .WithName("GroupShameWall")
            .Produces<IReadOnlyList<ShameItemResponse>>()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        app.MapGet("/api/v1/users/{id}/achievements", async (string id, ReputationService service, HttpContext context, CancellationToken cancellationToken) =>
                ApiResults.Ok(await service.ListAchievementsAsync(context.User.GetRequiredObjectId(), id, cancellationToken)))
            .RequireAuthorization()
            .WithTags("Achievements")
            .WithName("UserAchievements")
            .Produces<IReadOnlyList<AchievementResponse>>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
