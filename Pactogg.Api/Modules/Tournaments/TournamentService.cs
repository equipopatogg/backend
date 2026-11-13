using System.Security.Cryptography;
using MongoDB.Bson;
using Pactogg.Api.Common;
using Pactogg.Api.Infrastructure.Audit;
using Pactogg.Api.Infrastructure.Moderation;
using Pactogg.Api.Infrastructure.Redis;
using Pactogg.Api.Modules.Groups.Abstractions;
using Pactogg.Api.Modules.Groups.Domain;
using Pactogg.Api.Modules.Notifications;
using Pactogg.Api.Modules.Tournaments.Abstractions;
using Pactogg.Api.Modules.Tournaments.Domain;
using Pactogg.Api.Modules.Tournaments.Dtos;

namespace Pactogg.Api.Modules.Tournaments;

/// <summary>
/// Spec 010 (torneos entre grupos): TOR-01..09 y las lecturas MAT-01 y MAT-07.
/// MAT-02..06, el premio y la cancelación automática están en <see cref="MatchService"/>.
/// </summary>
public sealed class TournamentService
{
    public static readonly int[] AllowedSizes = [4, 8, 16];
    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const string ListScope = "tournaments";

    private static readonly HashSet<string> ValidStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "open",
        "in_progress",
        "finished",
        "cancelled"
    };

    private readonly ITournamentRepository _tournaments;
    private readonly IMatchRepository _matches;
    private readonly IGroupRepository _groups;
    private readonly IGroupMemberRepository _members;
    private readonly IModerationService _moderation;
    private readonly NotificationService _notifications;
    private readonly IAuditService _audit;
    private readonly ICacheService _cache;

    public TournamentService(
        ITournamentRepository tournaments,
        IMatchRepository matches,
        IGroupRepository groups,
        IGroupMemberRepository members,
        IModerationService moderation,
        NotificationService notifications,
        IAuditService audit,
        ICacheService cache)
    {
        _tournaments = tournaments;
        _matches = matches;
        _groups = groups;
        _members = members;
        _moderation = moderation;
        _notifications = notifications;
        _audit = audit;
        _cache = cache;
    }

    public static string TournamentScope(ObjectId id) => $"tournament:{id}";

    // ------------------------------------------------------------------ TOR-02

    public async Task<(IReadOnlyList<TournamentResponse> Items, object Meta)> ListAsync(
        ObjectId userId,
        string? status,
        string? game,
        string? groupId,
        int limit,
        string? cursor,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(status) && !ValidStatuses.Contains(status))
        {
            throw ApiException.Unprocessable(ErrorCodes.ValidationError, "status no es válido.");
        }

        if (limit is < 1 or > 50)
        {
            throw ApiException.Unprocessable(ErrorCodes.ValidationError, "limit debe estar entre 1 y 50.");
        }

        if (!string.IsNullOrWhiteSpace(game) && game.Length > 60)
        {
            throw ApiException.Unprocessable(ErrorCodes.ValidationError, "game es demasiado largo.");
        }

        ObjectId? groupFilter = null;
        if (!string.IsNullOrWhiteSpace(groupId))
        {
            if (!ObjectId.TryParse(groupId, out var parsedGroup))
            {
                throw ApiException.Unprocessable(ErrorCodes.ValidationError, "group_id no es válido.");
            }

            if (await _members.GetAsync(parsedGroup, userId, cancellationToken) is null)
            {
                throw ApiException.Forbidden(ErrorCodes.NotGroupMember, "No perteneces a este grupo.");
            }

            groupFilter = parsedGroup;
        }

        ObjectId? cursorId = null;
        if (!string.IsNullOrWhiteSpace(cursor))
        {
            if (!ObjectId.TryParse(cursor, out var parsedCursor))
            {
                throw ApiException.Unprocessable(ErrorCodes.ValidationError, "cursor no es válido.");
            }

            cursorId = parsedCursor;
        }

        var version = await _cache.GetVersionAsync(ListScope, cancellationToken);
        var snapshot = await _cache.GetOrCreateJsonAsync(
            $"tournaments:{version}:{userId}:{status}:{game}:{groupId}:{limit}:{cursor}",
            TimeSpan.FromSeconds(30),
            async () =>
            {
                var groupIds = await _members.ListActiveGroupIdsAsync(userId, cancellationToken);
                if (groupIds.Count == 0)
                {
                    return new PageSnapshot<TournamentResponse>([], null, false);
                }

                var (items, hasMore) = await _tournaments.ListForGroupsAsync(groupIds, status, game, groupFilter, limit, cursorId, cancellationToken);
                var matches = await _matches.ListForTournamentIdsAsync(items.Select(x => x.Id).ToArray(), cancellationToken);
                var byTournament = matches.GroupBy(x => x.TournamentId).ToDictionary(x => x.Key, x => (IReadOnlyList<Match>)x.ToList());
                var responses = items.Select(item => ToResponse(item, byTournament.GetValueOrDefault(item.Id, []), null, null)).ToList();
                var next = hasMore && items.Count > 0 ? items[^1].Id.ToString() : null;
                return new PageSnapshot<TournamentResponse>(responses, next, hasMore);
            },
            cancellationToken);

        return (snapshot.Items, new { next_cursor = snapshot.NextCursor, has_more = snapshot.HasMore });
    }

    // ------------------------------------------------------------------ TOR-01

    /// <summary>010-R1 y 010-D1: el administrador crea el torneo; su grupo queda inscrito y él paga la inscripción.</summary>
    public async Task<TournamentResponse> CreateAsync(ObjectId userId, string gid, CreateTournamentRequest request, CancellationToken cancellationToken)
    {
        var (group, _) = await RequireAdminAsync(userId, gid, cancellationToken);
        var now = DateTime.UtcNow;
        var game = request.Game.Trim();
        ValidateGame(game);
        var format = string.IsNullOrWhiteSpace(request.Format) ? "single_elimination" : request.Format.Trim();
        if (format != "single_elimination")
        {
            throw ApiException.Unprocessable(ErrorCodes.ValidationError, "Por ahora solo hay eliminación directa (single_elimination).");
        }

        var size = request.MaxGroups!.Value;
        ValidateSize(size);
        var scheduledAt = ValidateSchedule(request.ScheduledAt!.Value, now);
        await ModerateGameAsync(game, cancellationToken);

        if (await _tournaments.HasActiveForOrganizerGroupAsync(group.Id, cancellationToken))
        {
            throw ApiException.Conflict(ErrorCodes.TournamentAlreadyActive, "Tu grupo ya organiza un torneo abierto o en curso.");
        }

        var tournament = new Tournament
        {
            Id = ObjectId.GenerateNewId(),
            GroupId = group.Id,
            OrganizerId = userId,
            Code = await GenerateCodeAsync(cancellationToken),
            Game = game,
            Format = format,
            MaxGroups = size,
            EntryPoints = request.EntryPoints,
            PrizePool = request.EntryPoints,
            ScheduledAt = scheduledAt,
            Participants = [group.Id],
            Entries = [new TournamentEntry { GroupId = group.Id, PaidBy = userId, Amount = request.EntryPoints, Status = "active", JoinedAt = now }],
            Status = "open",
            CreatedAt = now,
            UpdatedAt = now
        };

        var result = await _tournaments.CreateAsync(tournament, now, cancellationToken);
        if (result.Outcome == TournamentWriteOutcome.InsufficientPoints)
        {
            throw ApiException.PaymentRequired(ErrorCodes.InsufficientPoints, "No tienes puntos suficientes para la inscripción.");
        }

        if (result.Outcome != TournamentWriteOutcome.Ok)
        {
            throw ApiException.Conflict(ErrorCodes.TournamentAlreadyActive, "No se pudo crear el torneo; inténtalo de nuevo.");
        }

        await _audit.AppendAsync("tournament.created", userId, "tournament", tournament.Id, group.Id, new { tournament_id = tournament.Id.ToString(), max_groups = size, entry_points = tournament.EntryPoints }, cancellationToken);
        await BumpAsync(tournament.Id, cancellationToken);
        return await BuildResponseAsync(result.Tournament!, userId, [], cancellationToken);
    }

    // ------------------------------------------------------------------ TOR-03

    /// <summary>
    /// 010-R2: detalle para miembros de los grupos inscritos. Con el código en la ruta, cualquier persona con sesión
    /// ve una vista previa para decidir si inscribe a su grupo (F2-D67); el código solo lo ven el organizador y los
    /// administradores inscritos.
    /// </summary>
    public async Task<TournamentResponse> GetAsync(ObjectId userId, string idOrCode, CancellationToken cancellationToken)
    {
        var (tournament, byCode) = await LoadAsync(idOrCode, cancellationToken);
        if (!byCode)
        {
            await RequireViewerAsync(userId, tournament, cancellationToken);
        }

        var matches = await _matches.ListForTournamentIdsAsync([tournament.Id], cancellationToken);
        return await BuildResponseAsync(tournament, userId, matches, cancellationToken);
    }

    // ------------------------------------------------------------------ TOR-04

    public async Task<TournamentResponse> UpdateAsync(ObjectId userId, string id, UpdateTournamentRequest request, CancellationToken cancellationToken)
    {
        var tournament = await RequireOrganizerAsync(userId, id, cancellationToken);
        if (request.Game is null && request.ScheduledAt is null && request.MaxGroups is null)
        {
            throw ApiException.Unprocessable(ErrorCodes.ValidationError, "Envía al menos game, scheduled_at o max_groups.");
        }

        RequireOpen(tournament);
        var now = DateTime.UtcNow;
        var game = request.Game?.Trim() ?? tournament.Game;
        ValidateGame(game);
        var scheduledAt = request.ScheduledAt is { } at ? ValidateSchedule(at, now) : tournament.ScheduledAt;
        var size = request.MaxGroups ?? tournament.MaxGroups;
        ValidateSize(size);
        if (size < tournament.Participants.Count)
        {
            throw ApiException.Unprocessable(ErrorCodes.ValidationError, $"Ya hay {tournament.Participants.Count} grupos inscritos; el tamaño no puede ser menor.");
        }

        if (request.Game is not null && game != tournament.Game)
        {
            await ModerateGameAsync(game, cancellationToken);
        }

        var updated = await _tournaments.UpdateAsync(tournament.Id, game, scheduledAt, size, now, cancellationToken)
            ?? throw ApiException.Conflict(ErrorCodes.TournamentNotOpen, "El torneo ya no está abierto o cambió el número de inscritos.");
        await _audit.AppendAsync("tournament.updated", userId, "tournament", tournament.Id, tournament.GroupId, new { tournament_id = tournament.Id.ToString() }, cancellationToken);
        await BumpAsync(tournament.Id, cancellationToken);
        return await BuildResponseAsync(updated, userId, [], cancellationToken);
    }

    // ------------------------------------------------------------------ TOR-05

    /// <summary>010-R4: el organizador cancela en open o in_progress; todos recuperan su inscripción.</summary>
    public async Task<TournamentResponse> CancelAsync(ObjectId userId, string id, CancellationToken cancellationToken)
    {
        var tournament = await RequireOrganizerAsync(userId, id, cancellationToken);
        if (tournament.Status is not ("open" or "in_progress"))
        {
            throw ApiException.Conflict(ErrorCodes.TournamentNotOpen, "El torneo ya terminó o ya estaba cancelado.");
        }

        var cancelled = await CancelInternalAsync(tournament, "by_organizer", userId, cancellationToken)
            ?? throw ApiException.Conflict(ErrorCodes.TournamentNotOpen, "El torneo ya terminó o ya estaba cancelado.");
        var matches = await _matches.ListForTournamentIdsAsync([cancelled.Id], cancellationToken);
        return await BuildResponseAsync(cancelled, userId, matches, cancellationToken);
    }

    /// <summary>Cancelación con devolución; la usan TOR-05 y la cancelación automática (010-R16).</summary>
    public async Task<Tournament?> CancelInternalAsync(Tournament tournament, string reason, ObjectId? actorId, CancellationToken cancellationToken)
    {
        var cancelled = await _tournaments.CancelAsync(tournament.Id, reason, DateTime.UtcNow, cancellationToken);
        if (cancelled is null)
        {
            return null;
        }

        await _audit.AppendAsync("tournament.cancelled", actorId, "tournament", tournament.Id, tournament.GroupId, new { tournament_id = tournament.Id.ToString(), reason }, cancellationToken);
        await BumpAsync(tournament.Id, cancellationToken);
        await NotifyGroupsAsync(
            cancelled.Entries.Select(x => x.GroupId).Distinct(),
            actorId,
            "tournament_cancelled",
            "Torneo cancelado",
            reason == "by_organizer"
                ? $"Se canceló el torneo de {cancelled.Game}. Se devolvió la inscripción."
                : $"El torneo de {cancelled.Game} se canceló porque quedaron menos de 2 grupos. Se devolvió la inscripción.",
            cancellationToken);
        return cancelled;
    }

    // ------------------------------------------------------------------ TOR-06

    /// <summary>010-R5 y 010-D2: el administrador de otro grupo lo inscribe con el código y paga de su saldo en ese grupo.</summary>
    public async Task<TournamentResponse> JoinAsync(ObjectId userId, string idOrCode, JoinTournamentRequest request, CancellationToken cancellationToken)
    {
        var (tournament, byCode) = await LoadAsync(idOrCode, cancellationToken);
        if (!byCode && !string.Equals(request.Code?.Trim(), tournament.Code, StringComparison.OrdinalIgnoreCase))
        {
            // Sin el código correcto no se revela si el torneo existe.
            throw ApiException.NotFound("El torneo no existe.");
        }

        var (group, _) = await RequireAdminAsync(userId, request.GroupId, cancellationToken);
        EnsureCanJoin(tournament, group.Id);

        var result = await _tournaments.JoinAsync(tournament.Id, tournament.MaxGroups, tournament.EntryPoints, group.Id, userId, DateTime.UtcNow, cancellationToken);
        if (result.Outcome == TournamentWriteOutcome.InsufficientPoints)
        {
            throw ApiException.PaymentRequired(ErrorCodes.InsufficientPoints, "No tienes puntos suficientes en este grupo para la inscripción.");
        }

        if (result.Outcome != TournamentWriteOutcome.Ok)
        {
            var fresh = await _tournaments.GetByIdAsync(tournament.Id, cancellationToken) ?? throw ApiException.NotFound("El torneo no existe.");
            EnsureCanJoin(fresh, group.Id);
            throw ApiException.Conflict(ErrorCodes.TournamentNotOpen, "El torneo cambió mientras te inscribías; inténtalo de nuevo.");
        }

        var joined = result.Tournament!;
        await _audit.AppendAsync("tournament.joined", userId, "tournament", joined.Id, group.Id, new { tournament_id = joined.Id.ToString(), entry_points = joined.EntryPoints }, cancellationToken);
        await BumpAsync(joined.Id, cancellationToken);
        await _notifications.NotifyAsync(
            [joined.OrganizerId],
            "tournament_joined",
            "Nuevo grupo en tu torneo",
            $"{group.Name} se inscribió al torneo de {joined.Game} ({joined.Participants.Count}/{joined.MaxGroups}).",
            joined.GroupId,
            null,
            null,
            cancellationToken);
        return await BuildResponseAsync(joined, userId, [], cancellationToken);
    }

    // ------------------------------------------------------------------ TOR-07

    /// <summary>010-R6: el administrador saca a su grupo mientras el torneo esté abierto y se le devuelve la inscripción.</summary>
    public async Task LeaveAsync(ObjectId userId, string id, string? groupId, CancellationToken cancellationToken)
    {
        var (tournament, _) = await LoadAsync(id, cancellationToken, allowCode: false);
        if (string.IsNullOrWhiteSpace(groupId))
        {
            throw ApiException.Unprocessable(ErrorCodes.ValidationError, "Falta group_id.");
        }

        var (group, _) = await RequireAdminAsync(userId, groupId, cancellationToken);
        if (group.Id == tournament.GroupId)
        {
            throw ApiException.Conflict(ErrorCodes.OrganizerGroupCannotLeave, "El grupo organizador no puede salir; el organizador puede cancelar el torneo.");
        }

        if (!tournament.Participants.Contains(group.Id))
        {
            throw ApiException.Conflict(ErrorCodes.NotInTournament, "Tu grupo no está inscrito en este torneo.");
        }

        RequireOpen(tournament);
        var left = await _tournaments.LeaveAsync(tournament.Id, group.Id, DateTime.UtcNow, cancellationToken)
            ?? throw ApiException.Conflict(ErrorCodes.TournamentNotOpen, "El torneo ya empezó; tu grupo ya no puede salir.");
        await _audit.AppendAsync("tournament.left", userId, "tournament", left.Id, group.Id, new { tournament_id = left.Id.ToString() }, cancellationToken);
        await BumpAsync(left.Id, cancellationToken);
    }

    // ------------------------------------------------------------------ TOR-08

    /// <summary>010-R7: con al menos 2 grupos, cierra inscripciones, sortea y genera el bracket con byes.</summary>
    public async Task<TournamentResponse> StartAsync(ObjectId userId, string id, CancellationToken cancellationToken)
    {
        var tournament = await RequireOrganizerAsync(userId, id, cancellationToken);
        RequireOpen(tournament);
        if (tournament.Participants.Count < 2)
        {
            throw ApiException.Conflict(ErrorCodes.NotEnoughGroups, "Se necesitan al menos 2 grupos inscritos para iniciar.");
        }

        var now = DateTime.UtcNow;
        var matches = BracketBuilder.Build(tournament.Id, tournament.Participants, new Random(RandomNumberGenerator.GetInt32(int.MaxValue)), now);
        var started = await _tournaments.StartAsync(tournament.Id, tournament.Participants, matches, now, cancellationToken)
            ?? throw ApiException.Conflict(ErrorCodes.TournamentNotOpen, "El torneo cambió (alguien se inscribió o salió); vuelve a intentarlo.");
        await _audit.AppendAsync("tournament.started", userId, "tournament", started.Id, started.GroupId, new { tournament_id = started.Id.ToString(), groups = started.Participants.Count, size = BracketBuilder.SizeFor(started.Participants.Count) }, cancellationToken);
        await BumpAsync(started.Id, cancellationToken);
        await NotifyGroupsAsync(
            started.Participants,
            userId,
            "tournament_started",
            "¡Arrancó el torneo!",
            $"Ya está el bracket del torneo de {started.Game}. Revisa contra quién juega tu grupo.",
            cancellationToken);
        return await BuildResponseAsync(started, userId, matches, cancellationToken);
    }

    // ------------------------------------------------------------------ MAT-01, MAT-02, MAT-07

    /// <summary>MAT-01 (010-R9): bracket completo para los miembros de los grupos inscritos. Caché 10 s.</summary>
    public async Task<BracketResponse> GetBracketAsync(ObjectId userId, string id, CancellationToken cancellationToken)
    {
        var (tournament, _) = await LoadAsync(id, cancellationToken, allowCode: false);
        await RequireViewerAsync(userId, tournament, cancellationToken);
        var version = await _cache.GetVersionAsync(TournamentScope(tournament.Id), cancellationToken);
        return await _cache.GetOrCreateJsonAsync(
            $"bracket:{tournament.Id}:{version}",
            TimeSpan.FromSeconds(10),
            async () => ToBracket(await _matches.ListForTournamentIdsAsync([tournament.Id], cancellationToken)),
            cancellationToken);
    }

    /// <summary>MAT-07 (010-R15): la partida pendiente más próxima de cualquiera de mis grupos, o null.</summary>
    public async Task<NextMatchResponse?> GetNextMatchAsync(ObjectId userId, CancellationToken cancellationToken)
    {
        var groupIds = await _members.ListActiveGroupIdsAsync(userId, cancellationToken);
        var match = await _matches.FindNextForGroupsAsync(groupIds, cancellationToken);
        if (match is null)
        {
            return null;
        }

        var tournament = await _tournaments.GetByIdAsync(match.TournamentId, cancellationToken);
        if (tournament is null || tournament.Status != "in_progress")
        {
            return null;
        }

        var mine = groupIds.Contains(match.P1GroupId!.Value) ? match.P1GroupId.Value : match.P2GroupId!.Value;
        var opponent = mine == match.P1GroupId ? match.P2GroupId : match.P1GroupId;
        return new NextMatchResponse
        {
            Match = ToMatch(match),
            MyGroupId = mine.ToString(),
            OpponentGroupId = opponent?.ToString(),
            Game = tournament.Game,
            ScheduledAt = tournament.ScheduledAt
        };
    }

    // ------------------------------------------------------------------ apoyo

    private async Task<(Tournament Tournament, bool ByCode)> LoadAsync(string idOrCode, CancellationToken cancellationToken, bool allowCode = true)
    {
        if (ObjectId.TryParse(idOrCode, out var id))
        {
            return (await _tournaments.GetByIdAsync(id, cancellationToken) ?? throw ApiException.NotFound("El torneo no existe."), false);
        }

        if (allowCode && idOrCode.Length == 6)
        {
            var byCode = await _tournaments.GetByCodeAsync(idOrCode.ToUpperInvariant(), cancellationToken);
            if (byCode is not null)
            {
                return (byCode, true);
            }
        }

        throw ApiException.NotFound("El torneo no existe.");
    }

    private async Task<Tournament> RequireOrganizerAsync(ObjectId userId, string id, CancellationToken cancellationToken)
    {
        var (tournament, _) = await LoadAsync(id, cancellationToken, allowCode: false);
        if (tournament.OrganizerId != userId)
        {
            throw ApiException.Forbidden(ErrorCodes.NotTournamentOrganizer, "Solo quien organiza el torneo puede hacer esto.");
        }

        return tournament;
    }

    private async Task<(Group Group, GroupMember Member)> RequireAdminAsync(ObjectId userId, string groupId, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(groupId, out var gid))
        {
            throw ApiException.NotFound("El grupo no existe.");
        }

        var group = await _groups.GetByIdAsync(gid, cancellationToken);
        if (group is null || group.DeletedAt is not null)
        {
            throw ApiException.NotFound("El grupo no existe.");
        }

        var member = await _members.GetAsync(gid, userId, cancellationToken)
            ?? throw ApiException.Forbidden(ErrorCodes.NotGroupMember, "No perteneces a este grupo.");
        if (member.Role != "admin")
        {
            throw ApiException.Forbidden(ErrorCodes.NotGroupAdmin, "Solo un administrador del grupo puede inscribirlo en torneos.");
        }

        return (group, member);
    }

    /// <summary>Miembro activo de un grupo inscrito (incluye al organizador y a su grupo).</summary>
    private async Task RequireViewerAsync(ObjectId userId, Tournament tournament, CancellationToken cancellationToken)
    {
        if (tournament.OrganizerId == userId)
        {
            return;
        }

        var mine = await _members.ListActiveGroupIdsAsync(userId, cancellationToken);
        if (!mine.Contains(tournament.GroupId) && !tournament.Participants.Any(mine.Contains))
        {
            throw ApiException.Forbidden(ErrorCodes.NotTournamentMember, "Tu grupo no participa en este torneo.");
        }
    }

    private static void EnsureCanJoin(Tournament tournament, ObjectId groupId)
    {
        if (tournament.Participants.Contains(groupId))
        {
            throw ApiException.Conflict(ErrorCodes.AlreadyInTournament, "Tu grupo ya está inscrito.");
        }

        RequireOpen(tournament);
        if (tournament.Participants.Count >= tournament.MaxGroups)
        {
            throw ApiException.Conflict(ErrorCodes.TournamentFull, "El torneo ya está lleno.");
        }
    }

    private static void RequireOpen(Tournament tournament)
    {
        if (tournament.Status != "open")
        {
            throw ApiException.Conflict(ErrorCodes.TournamentNotOpen, "El torneo ya no acepta cambios (ya empezó, terminó o se canceló).");
        }
    }

    private static void ValidateGame(string game)
    {
        if (game.Length is < 1 or > 60)
        {
            throw ApiException.Unprocessable(ErrorCodes.ValidationError, "game debe tener de 1 a 60 caracteres.");
        }
    }

    private static void ValidateSize(int size)
    {
        if (!AllowedSizes.Contains(size))
        {
            throw ApiException.Unprocessable(ErrorCodes.ValidationError, "max_groups debe ser 4, 8 o 16.");
        }
    }

    private static DateTime ValidateSchedule(DateTime value, DateTime now)
    {
        var utc = value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(value, DateTimeKind.Utc) : value.ToUniversalTime();
        if (utc <= now)
        {
            throw ApiException.Unprocessable(ErrorCodes.ValidationError, "scheduled_at debe ser una fecha futura.");
        }

        return utc;
    }

    private async Task ModerateGameAsync(string game, CancellationToken cancellationToken)
    {
        var moderation = await _moderation.ModerateTextAsync(game, cancellationToken);
        if (!moderation.Approved)
        {
            throw ApiException.Unprocessable(ErrorCodes.ContentRejected, "El nombre del juego fue rechazado por moderación.", new { labels = moderation.Labels });
        }
    }

    private async Task<string> GenerateCodeAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var chars = new char[6];
            for (var i = 0; i < chars.Length; i++)
            {
                chars[i] = CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)];
            }

            var code = new string(chars);
            if (!await _tournaments.CodeExistsAsync(code, cancellationToken))
            {
                return code;
            }
        }

        throw ApiException.ServiceUnavailable(ErrorCodes.DatabaseUnavailable, "No se pudo generar el código del torneo; inténtalo de nuevo.");
    }

    internal async Task BumpAsync(ObjectId tournamentId, CancellationToken cancellationToken)
    {
        await _cache.BumpVersionAsync(TournamentScope(tournamentId), cancellationToken);
        await _cache.BumpVersionAsync(ListScope, cancellationToken);
    }

    internal async Task NotifyGroupsAsync(IEnumerable<ObjectId> groupIds, ObjectId? exclude, string type, string title, string body, CancellationToken cancellationToken, string? dedupeSuffix = null)
    {
        foreach (var groupId in groupIds.Distinct())
        {
            var (members, _) = await _members.ListByGroupAsync(groupId, null, 50, null, cancellationToken);
            var recipients = members.Select(x => x.UserId).Where(x => x != exclude).ToArray();
            if (recipients.Length > 0)
            {
                await _notifications.NotifyAsync(recipients, type, title, body, groupId, null, null, cancellationToken, dedupeSuffix);
            }
        }
    }

    private async Task<TournamentResponse> BuildResponseAsync(Tournament tournament, ObjectId viewerId, IReadOnlyList<Match> matches, CancellationToken cancellationToken)
    {
        var groups = new List<TournamentGroupResponse>();
        foreach (var groupId in tournament.Participants)
        {
            var group = await _groups.GetByIdAsync(groupId, cancellationToken);
            if (group is not null)
            {
                groups.Add(new TournamentGroupResponse
                {
                    GroupId = group.Id.ToString(),
                    Name = group.DeletedAt is null ? group.Name : "Grupo eliminado",
                    MemberCount = group.MemberCount,
                    IsOrganizer = group.Id == tournament.GroupId
                });
            }
        }

        string? code = null;
        if (tournament.Status == "open")
        {
            var canSeeCode = tournament.OrganizerId == viewerId;
            if (!canSeeCode)
            {
                foreach (var groupId in tournament.Participants)
                {
                    if (await _members.GetAsync(groupId, viewerId, cancellationToken) is { Role: "admin" })
                    {
                        canSeeCode = true;
                        break;
                    }
                }
            }

            code = canSeeCode ? tournament.Code : null;
        }

        return ToResponse(tournament, matches, groups, code);
    }

    private static TournamentResponse ToResponse(Tournament tournament, IReadOnlyList<Match> matches, IReadOnlyList<TournamentGroupResponse>? groups, string? code) => new()
    {
        Id = tournament.Id.ToString(),
        GroupId = tournament.GroupId.ToString(),
        OrganizerId = tournament.OrganizerId.ToString(),
        Game = tournament.Game,
        Format = tournament.Format,
        MaxGroups = tournament.MaxGroups,
        EntryPoints = tournament.EntryPoints,
        PrizePool = tournament.PrizePool,
        ScheduledAt = tournament.ScheduledAt,
        Participants = tournament.Participants.Select(x => x.ToString()).ToList(),
        Groups = groups,
        Status = tournament.Status,
        WinnerGroupId = tournament.WinnerGroupId?.ToString(),
        Code = code,
        StartedAt = tournament.StartedAt,
        CancelledAt = tournament.CancelledAt,
        CancelReason = tournament.CancelReason,
        CreatedAt = tournament.CreatedAt,
        UpdatedAt = tournament.UpdatedAt,
        Bracket = ToBracket(matches)
    };

    private static BracketResponse ToBracket(IReadOnlyList<Match> matches)
    {
        var ordered = matches.OrderBy(x => x.Round).ThenBy(x => x.Position).ToList();
        var firstRound = ordered.Count(x => x.Round == 1);
        return new BracketResponse
        {
            Size = firstRound * 2,
            Rounds = ordered.Count == 0 ? 0 : ordered.Max(x => x.Round),
            Matches = ordered.Select(x => ToMatch(x)).ToList()
        };
    }

    internal static MatchResponse ToMatch(Match match, string? screenshotUrl = null) => new()
    {
        Id = match.Id.ToString(),
        TournamentId = match.TournamentId.ToString(),
        Round = match.Round,
        Position = match.Position,
        P1GroupId = match.P1GroupId?.ToString(),
        P2GroupId = match.P2GroupId?.ToString(),
        P1Score = match.P1Score,
        P2Score = match.P2Score,
        WinnerGroupId = match.WinnerGroupId?.ToString(),
        Status = match.Status,
        NextMatchId = match.NextMatchId?.ToString(),
        ReportedAt = match.ReportedAt,
        ReportedByGroupId = match.ReportedByGroupId?.ToString(),
        DisputeReason = match.DisputeReason,
        Resolution = match.Resolution,
        ConfirmedAt = match.ConfirmedAt,
        ScreenshotUrl = screenshotUrl,
        UpdatedAt = match.UpdatedAt
    };
}
