using Microsoft.Extensions.Options;
using MongoDB.Bson;
using Pactogg.Api.Common;
using Pactogg.Api.Infrastructure.Audit;
using Pactogg.Api.Infrastructure.Moderation;
using Pactogg.Api.Infrastructure.Options;
using Pactogg.Api.Infrastructure.Storage;
using Pactogg.Api.Modules.Evidence;
using Pactogg.Api.Modules.Groups.Abstractions;
using Pactogg.Api.Modules.Notifications;
using Pactogg.Api.Modules.Reputation;
using Pactogg.Api.Modules.Tournaments.Abstractions;
using Pactogg.Api.Modules.Tournaments.Domain;
using Pactogg.Api.Modules.Tournaments.Dtos;

namespace Pactogg.Api.Modules.Tournaments;

/// <summary>
/// Spec 010, parte 2: MAT-02..06 (010-R9..R13), la final con reparto de la bolsa y trofeo (010-R14),
/// la cancelación automática con menos de 2 grupos (010-R16) y TOR-09 (010-R8).
/// </summary>
public sealed class MatchService
{
    public static readonly TimeSpan NoAnswerWindow = TimeSpan.FromHours(24);

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp"
    };

    private readonly ITournamentRepository _tournaments;
    private readonly IMatchRepository _matches;
    private readonly IGroupRepository _groups;
    private readonly IGroupMemberRepository _members;
    private readonly IModerationService _moderation;
    private readonly IObjectStorage _storage;
    private readonly EvidenceOptions _evidenceOptions;
    private readonly NotificationService _notifications;
    private readonly IAuditService _audit;
    private readonly ReputationService _reputation;
    private readonly TournamentService _tournamentService;

    public MatchService(
        ITournamentRepository tournaments,
        IMatchRepository matches,
        IGroupRepository groups,
        IGroupMemberRepository members,
        IModerationService moderation,
        IObjectStorage storage,
        IOptions<EvidenceOptions> evidenceOptions,
        NotificationService notifications,
        IAuditService audit,
        ReputationService reputation,
        TournamentService tournamentService)
    {
        _tournaments = tournaments;
        _matches = matches;
        _groups = groups;
        _members = members;
        _moderation = moderation;
        _storage = storage;
        _evidenceOptions = evidenceOptions.Value;
        _notifications = notifications;
        _audit = audit;
        _reputation = reputation;
        _tournamentService = tournamentService;
    }

    // ------------------------------------------------------------------ MAT-02

    /// <summary>MAT-02 (010-R9): detalle de la partida con la foto del resultado (URL firmada 15 min).</summary>
    public async Task<MatchResponse> GetAsync(ObjectId userId, string matchId, CancellationToken cancellationToken)
    {
        var (match, tournament) = await LoadAsync(matchId, cancellationToken);
        await RequireViewerAsync(userId, tournament, cancellationToken);
        return await ToResponseAsync(match, cancellationToken);
    }

    // ------------------------------------------------------------------ MAT-03

    /// <summary>
    /// MAT-03 (010-R10): un miembro de uno de los dos grupos reporta marcador y foto moderada.
    /// El empate se rechaza (se repite la partida); quien reporta queda como representante de su grupo (010-D4).
    /// </summary>
    public async Task<MatchResponse> ReportAsync(
        ObjectId userId,
        string matchId,
        int p1Score,
        int p2Score,
        string? groupId,
        string? winnerGroupId,
        byte[] content,
        string contentType,
        CancellationToken cancellationToken)
    {
        var (match, tournament) = await LoadAsync(matchId, cancellationToken);
        var side = await RequirePlayerAsync(userId, match, groupId, cancellationToken);
        RequireInProgress(tournament);
        if (match.Status != "pending" || match.P1GroupId is null || match.P2GroupId is null)
        {
            throw ApiException.Conflict(ErrorCodes.InvalidMatchState, "Esta partida no se puede reportar (ya tiene resultado o falta un rival).");
        }

        if (p1Score is < 0 or > 999 || p2Score is < 0 or > 999)
        {
            throw ApiException.Unprocessable(ErrorCodes.ValidationError, "El marcador va de 0 a 999.");
        }

        if (p1Score == p2Score)
        {
            throw ApiException.Unprocessable(ErrorCodes.TieNotAllowed, "No hay empates: repitan la partida y reporten al ganador.");
        }

        var winner = p1Score > p2Score ? match.P1GroupId.Value : match.P2GroupId.Value;
        if (!string.IsNullOrWhiteSpace(winnerGroupId) && (!ObjectId.TryParse(winnerGroupId, out var declared) || declared != winner))
        {
            throw ApiException.Unprocessable(ErrorCodes.ValidationError, "winner_group_id no coincide con el marcador.");
        }

        var normalized = await ValidatePhotoAsync(content, contentType, cancellationToken);
        var now = DateTime.UtcNow;
        var key = await _storage.PutAsync(normalized, "image/jpeg", $"evidence/matches/{tournament.Id}/{match.Id}-{ObjectId.GenerateNewId()}.jpg", cancellationToken);
        var updated = await _matches.TryReportAsync(match.Id, p1Score, p2Score, winner, userId, side.GroupId, side.IsP1, key, now, cancellationToken);
        if (updated is null)
        {
            await TryDeleteAsync(key, cancellationToken);
            throw ApiException.Conflict(ErrorCodes.InvalidMatchState, "Alguien ya reportó esta partida.");
        }

        await _audit.AppendAsync("match.reported", userId, "match", match.Id, side.GroupId, new { tournament_id = tournament.Id.ToString(), p1_score = p1Score, p2_score = p2Score }, cancellationToken);
        var rival = side.IsP1 ? match.P2GroupId.Value : match.P1GroupId.Value;
        await _tournamentService.NotifyGroupsAsync(
            [rival],
            null,
            "tournament_match_reported",
            $"Resultado reportado en {tournament.Game}",
            $"Marcador {p1Score}-{p2Score}. Confirma o disputa el resultado.",
            cancellationToken,
            match.Id.ToString());
        await _tournamentService.BumpAsync(tournament.Id, cancellationToken);
        return await ToResponseAsync(updated, cancellationToken);
    }

    // ------------------------------------------------------------------ MAT-04

    /// <summary>MAT-04 (010-R11): el grupo rival confirma; el ganador avanza y, si era la final, cierra el torneo.</summary>
    public async Task<MatchResponse> ConfirmAsync(ObjectId userId, string matchId, CancellationToken cancellationToken)
    {
        var (match, tournament) = await LoadAsync(matchId, cancellationToken);
        var side = await RequireRivalOfReportAsync(userId, match, cancellationToken);
        RequireInProgress(tournament);
        if (match.Status != "reported")
        {
            throw ApiException.Conflict(ErrorCodes.InvalidMatchState, "Solo se confirma un resultado reportado.");
        }

        var now = DateTime.UtcNow;
        var updated = await _matches.TryConfirmAsync(match.Id, ["reported"], match.WinnerGroupId!.Value, false, userId, side.IsP1, null, "confirmed_by_rival", now, cancellationToken)
            ?? throw ApiException.Conflict(ErrorCodes.InvalidMatchState, "El resultado ya cambió; recarga la partida.");
        await _audit.AppendAsync("match.confirmed", userId, "match", match.Id, side.GroupId, new { tournament_id = tournament.Id.ToString() }, cancellationToken);
        await AdvanceAsync(tournament, updated, userId, now, cancellationToken);
        return await ToResponseAsync(updated, cancellationToken);
    }

    // ------------------------------------------------------------------ MAT-05

    /// <summary>MAT-05 (010-R12): el grupo rival disputa con motivo moderado; se avisa al organizador.</summary>
    public async Task<MatchResponse> DisputeAsync(ObjectId userId, string matchId, DisputeMatchRequest request, CancellationToken cancellationToken)
    {
        var (match, tournament) = await LoadAsync(matchId, cancellationToken);
        var side = await RequireRivalOfReportAsync(userId, match, cancellationToken);
        RequireInProgress(tournament);
        if (match.Status != "reported")
        {
            throw ApiException.Conflict(ErrorCodes.InvalidMatchState, "Solo se disputa un resultado reportado.");
        }

        var reason = request.Reason.Trim();
        if (reason.Length is < 5 or > 140)
        {
            throw ApiException.Unprocessable(ErrorCodes.ValidationError, "El motivo lleva de 5 a 140 caracteres.");
        }

        var moderation = await _moderation.ModerateTextAsync(reason, cancellationToken);
        if (!moderation.Approved)
        {
            throw ApiException.Unprocessable(ErrorCodes.ContentRejected, "El motivo fue rechazado por moderación.", new { labels = moderation.Labels });
        }

        var now = DateTime.UtcNow;
        var updated = await _matches.TryDisputeAsync(match.Id, userId, side.IsP1, reason, now, cancellationToken)
            ?? throw ApiException.Conflict(ErrorCodes.InvalidMatchState, "El resultado ya cambió; recarga la partida.");
        await _audit.AppendAsync("match.disputed", userId, "match", match.Id, side.GroupId, new { tournament_id = tournament.Id.ToString() }, cancellationToken);
        await _notifications.NotifyAsync(
            [tournament.OrganizerId],
            "tournament_match_disputed",
            $"Disputa en {tournament.Game}",
            "Un grupo no está de acuerdo con el resultado. Revisa la foto y decide quién ganó.",
            tournament.GroupId,
            null,
            null,
            cancellationToken,
            match.Id.ToString());
        await _tournamentService.BumpAsync(tournament.Id, cancellationToken);
        return await ToResponseAsync(updated, cancellationToken);
    }

    // ------------------------------------------------------------------ MAT-06

    /// <summary>
    /// MAT-06 (010-R13): el organizador fija el ganador cuando hay disputa, cuando un reporte lleva 24 h
    /// sin respuesta o por abandono (partida pendiente con los dos grupos). Si juega esa partida, 409.
    /// </summary>
    public async Task<MatchResponse> ResolveAsync(ObjectId userId, string matchId, ResolveMatchRequest request, CancellationToken cancellationToken)
    {
        var (match, tournament) = await LoadAsync(matchId, cancellationToken);
        if (tournament.OrganizerId != userId)
        {
            throw ApiException.Forbidden(ErrorCodes.NotTournamentOrganizer, "Solo quien organiza el torneo resuelve partidas.");
        }

        RequireInProgress(tournament);
        if (match.P1GroupId is null || match.P2GroupId is null)
        {
            throw ApiException.Conflict(ErrorCodes.InvalidMatchState, "A esta partida le falta un rival.");
        }

        if (await _members.GetAsync(match.P1GroupId.Value, userId, cancellationToken) is not null
            || await _members.GetAsync(match.P2GroupId.Value, userId, cancellationToken) is not null)
        {
            throw ApiException.Conflict(ErrorCodes.OrganizerPlaysMatch, "Juegas en esta partida: no puedes resolverla tú (CLARIFICACION #105).");
        }

        if (!ObjectId.TryParse(request.WinnerGroupId, out var winner) || (winner != match.P1GroupId && winner != match.P2GroupId))
        {
            throw ApiException.Unprocessable(ErrorCodes.ValidationError, "winner_group_id debe ser uno de los dos grupos de la partida.");
        }

        var now = DateTime.UtcNow;
        string resolution;
        switch (match.Status)
        {
            case "disputed":
                resolution = "resolved_dispute";
                break;
            case "reported" when match.ReportedAt is DateTime reportedAt && now - reportedAt >= NoAnswerWindow:
                resolution = "resolved_no_answer";
                break;
            case "reported":
                throw ApiException.Conflict(ErrorCodes.ReportTooRecent, "El rival todavía tiene 24 h para confirmar o disputar.");
            case "pending":
                resolution = "abandonment";
                break;
            default:
                throw ApiException.Conflict(ErrorCodes.InvalidMatchState, "Esta partida ya está cerrada.");
        }

        var reason = request.Reason?.Trim();
        if (!string.IsNullOrEmpty(reason))
        {
            var moderation = await _moderation.ModerateTextAsync(reason, cancellationToken);
            if (!moderation.Approved)
            {
                throw ApiException.Unprocessable(ErrorCodes.ContentRejected, "El motivo fue rechazado por moderación.", new { labels = moderation.Labels });
            }
        }

        // El marcador reportado solo se conserva si el organizador le da la razón a quien reportó.
        var clearScores = resolution == "abandonment" || winner != match.WinnerGroupId;
        var updated = await _matches.TryConfirmAsync(match.Id, [match.Status], winner, clearScores, null, false, userId, resolution, now, cancellationToken)
            ?? throw ApiException.Conflict(ErrorCodes.InvalidMatchState, "La partida ya cambió; recarga.");
        await _audit.AppendAsync("match.resolved", userId, "match", match.Id, tournament.GroupId, new { tournament_id = tournament.Id.ToString(), resolution, winner_group_id = winner.ToString(), reason }, cancellationToken);
        await _tournamentService.NotifyGroupsAsync(
            [match.P1GroupId.Value, match.P2GroupId.Value],
            null,
            "tournament_match_resolved",
            $"El organizador resolvió una partida de {tournament.Game}",
            string.IsNullOrEmpty(reason) ? "Revisa el bracket para ver quién avanza." : reason,
            cancellationToken,
            match.Id.ToString());
        await AdvanceAsync(tournament, updated, userId, now, cancellationToken);
        return await ToResponseAsync(updated, cancellationToken);
    }

    // ------------------------------------------------------------------ 010-R14

    private async Task AdvanceAsync(Tournament tournament, Match match, ObjectId actorId, DateTime now, CancellationToken cancellationToken)
    {
        var winner = match.WinnerGroupId!.Value;
        if (match.NextMatchId is ObjectId next)
        {
            await _matches.PlaceWinnerAsync(next, match.Position % 2 == 0, winner, now, cancellationToken);
            await _tournamentService.BumpAsync(tournament.Id, cancellationToken);
            return;
        }

        await FinishAsync(tournament, winner, actorId, now, cancellationToken);
    }

    /// <summary>
    /// 010-R14: la bolsa se reparte en partes iguales entre los representantes del campeón (orden de juego;
    /// el residuo al primero). Si el campeón no tuvo representante (ganó por resolución del organizador),
    /// la bolsa va a quien pagó su inscripción (F2-D71).
    /// </summary>
    private async Task FinishAsync(Tournament tournament, ObjectId champion, ObjectId actorId, DateTime now, CancellationToken cancellationToken)
    {
        var matches = await _matches.ListForTournamentIdsAsync([tournament.Id], cancellationToken);
        var recipients = new List<ObjectId>();
        foreach (var played in matches.Where(x => x.Status == "confirmed").OrderBy(x => x.Round).ThenBy(x => x.Position))
        {
            var reps = played.P1GroupId == champion ? played.P1Representatives : played.P2GroupId == champion ? played.P2Representatives : [];
            foreach (var rep in reps.Where(x => !recipients.Contains(x)))
            {
                recipients.Add(rep);
            }
        }

        if (recipients.Count == 0)
        {
            var payer = tournament.Entries.FirstOrDefault(x => x.GroupId == champion)?.PaidBy;
            if (payer is ObjectId p)
            {
                recipients.Add(p);
            }
        }

        // Solo cobra quien sigue en el grupo campeón; si nadie sigue, la bolsa no se paga a nadie fuera del grupo.
        var active = new List<ObjectId>();
        foreach (var userId in recipients)
        {
            if (await _members.GetAsync(champion, userId, cancellationToken) is not null)
            {
                active.Add(userId);
            }
        }

        var prizes = Split(tournament.PrizePool, active);
        var finished = await _tournaments.FinishAsync(tournament.Id, champion, prizes, now, cancellationToken);
        if (finished is null)
        {
            return;
        }

        foreach (var userId in active)
        {
            await _reputation.AwardTournamentChampionAsync(userId, champion, tournament.Id, now, cancellationToken);
        }

        var championGroup = await _groups.GetByIdAsync(champion, cancellationToken);
        await _audit.AppendAsync("tournament.finished", actorId, "tournament", tournament.Id, tournament.GroupId, new
        {
            tournament_id = tournament.Id.ToString(),
            winner_group_id = champion.ToString(),
            prize_pool = tournament.PrizePool,
            recipients = prizes.Count
        }, cancellationToken);
        await _tournamentService.NotifyGroupsAsync(
            tournament.Participants,
            null,
            "tournament_finished",
            $"Terminó el torneo de {tournament.Game}",
            $"Campeón: {championGroup?.Name ?? "un grupo"}. Bolsa de {tournament.PrizePool} puntos.",
            cancellationToken,
            tournament.Id.ToString());
        await _tournamentService.BumpAsync(tournament.Id, cancellationToken);
    }

    /// <summary>Partes iguales; el residuo al primero en orden de juego (010-R14).</summary>
    public static IReadOnlyList<(ObjectId UserId, int Amount)> Split(int pool, IReadOnlyList<ObjectId> recipients)
    {
        if (recipients.Count == 0 || pool <= 0)
        {
            return [];
        }

        var share = pool / recipients.Count;
        var remainder = pool - share * recipients.Count;
        return recipients.Select((userId, i) => (userId, share + (i == 0 ? remainder : 0))).ToList();
    }

    // ------------------------------------------------------------------ 010-R16

    /// <summary>010-R16 (CLARIFICACION #101): al eliminar un grupo, cancela los torneos en curso que quedan con menos de 2 grupos activos.</summary>
    public async Task<int> OnGroupDeletedAsync(ObjectId groupId, ObjectId actorId, CancellationToken cancellationToken)
    {
        var cancelled = 0;
        foreach (var tournament in await _tournaments.ListActiveForGroupAsync(groupId, cancellationToken))
        {
            if (tournament.Status != "in_progress")
            {
                continue;
            }

            var alive = 0;
            foreach (var participant in tournament.Participants)
            {
                if (await _groups.GetByIdAsync(participant, cancellationToken) is { DeletedAt: null })
                {
                    alive++;
                }
            }

            if (alive < 2 && await _tournamentService.CancelInternalAsync(tournament, "not_enough_groups", actorId, cancellationToken) is not null)
            {
                cancelled++;
            }
        }

        return cancelled;
    }

    // ------------------------------------------------------------------ TOR-09

    /// <summary>
    /// TOR-09 (010-R8, 010-D3): el organizador invita grupos; solo se avisa a los administradores de grupos
    /// donde él también es miembro activo. Los demás reciben el código fuera de la app.
    /// </summary>
    public async Task<InviteGroupsResponse> InviteAsync(ObjectId userId, string tournamentId, InviteGroupsRequest request, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(tournamentId, out var id))
        {
            throw ApiException.NotFound("El torneo no existe.");
        }

        var tournament = await _tournaments.GetByIdAsync(id, cancellationToken) ?? throw ApiException.NotFound("El torneo no existe.");
        if (tournament.OrganizerId != userId)
        {
            throw ApiException.Forbidden(ErrorCodes.NotTournamentOrganizer, "Solo quien organiza el torneo puede invitar.");
        }

        if (tournament.Status != "open")
        {
            throw ApiException.Conflict(ErrorCodes.TournamentNotOpen, "El torneo ya no acepta inscripciones.");
        }

        var invited = new List<string>();
        var skipped = new List<InviteSkippedResponse>();
        foreach (var raw in request.GroupIds.Distinct())
        {
            if (!ObjectId.TryParse(raw, out var groupId) || await _groups.GetByIdAsync(groupId, cancellationToken) is not { DeletedAt: null } group)
            {
                skipped.Add(new InviteSkippedResponse { GroupId = raw, Reason = "not_found" });
                continue;
            }

            if (tournament.Participants.Contains(groupId))
            {
                skipped.Add(new InviteSkippedResponse { GroupId = raw, Reason = "already_joined" });
                continue;
            }

            // Sin membresía no se revela si el grupo existe: misma razón que un grupo ajeno.
            if (await _members.GetAsync(groupId, userId, cancellationToken) is null)
            {
                skipped.Add(new InviteSkippedResponse { GroupId = raw, Reason = "not_member" });
                continue;
            }

            var (members, _) = await _members.ListByGroupAsync(groupId, null, 50, null, cancellationToken);
            var admins = members.Where(x => x.Role == "admin" && x.UserId != userId).Select(x => x.UserId).ToArray();
            if (admins.Length > 0)
            {
                await _notifications.NotifyAsync(
                    admins,
                    "tournament_invite",
                    $"Invitación a un torneo de {tournament.Game}",
                    $"Inscribe a {group.Name} con el código {tournament.Code}. Entrada: {tournament.EntryPoints} puntos.",
                    groupId,
                    null,
                    null,
                    cancellationToken,
                    tournament.Id.ToString());
            }

            invited.Add(groupId.ToString());
        }

        await _audit.AppendAsync("tournament.invited", userId, "tournament", tournament.Id, tournament.GroupId, new { invited = invited.Count, skipped = skipped.Count }, cancellationToken);
        return new InviteGroupsResponse { Invited = invited, Skipped = skipped, Code = tournament.Code ?? string.Empty };
    }

    // ------------------------------------------------------------------ apoyo

    private sealed record Side(ObjectId GroupId, bool IsP1);

    private async Task<(Match Match, Tournament Tournament)> LoadAsync(string matchId, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(matchId, out var id))
        {
            throw ApiException.NotFound("La partida no existe.");
        }

        var match = await _matches.GetByIdAsync(id, cancellationToken) ?? throw ApiException.NotFound("La partida no existe.");
        var tournament = await _tournaments.GetByIdAsync(match.TournamentId, cancellationToken) ?? throw ApiException.NotFound("La partida no existe.");
        return (match, tournament);
    }

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

    /// <summary>Miembro activo de P1 o P2; si es de los dos grupos, group_id decide por cuál juega.</summary>
    private async Task<Side> RequirePlayerAsync(ObjectId userId, Match match, string? groupId, CancellationToken cancellationToken)
    {
        var inP1 = match.P1GroupId is ObjectId p1 && await _members.GetAsync(p1, userId, cancellationToken) is not null;
        var inP2 = match.P2GroupId is ObjectId p2 && await _members.GetAsync(p2, userId, cancellationToken) is not null;
        if (!inP1 && !inP2)
        {
            throw ApiException.Forbidden(ErrorCodes.NotMatchPlayer, "Solo los grupos de esta partida pueden reportarla.");
        }

        if (inP1 && inP2)
        {
            if (string.IsNullOrWhiteSpace(groupId) || !ObjectId.TryParse(groupId, out var chosen) || (chosen != match.P1GroupId && chosen != match.P2GroupId))
            {
                throw ApiException.Unprocessable(ErrorCodes.ValidationError, "Eres de los dos grupos: indica group_id para decir por cuál juegas.");
            }

            return new Side(chosen, chosen == match.P1GroupId);
        }

        return inP1 ? new Side(match.P1GroupId!.Value, true) : new Side(match.P2GroupId!.Value, false);
    }

    /// <summary>Confirma o disputa un miembro del grupo que NO reportó, y nunca quien reportó.</summary>
    private async Task<Side> RequireRivalOfReportAsync(ObjectId userId, Match match, CancellationToken cancellationToken)
    {
        if (match.Status is not ("reported" or "disputed") || match.ReportedByGroupId is null)
        {
            throw ApiException.Conflict(ErrorCodes.InvalidMatchState, "Esta partida no tiene un resultado por confirmar.");
        }

        var rivalGroup = match.ReportedByGroupId == match.P1GroupId ? match.P2GroupId!.Value : match.P1GroupId!.Value;
        if (match.ReportedBy == userId)
        {
            throw ApiException.Forbidden(ErrorCodes.CannotConfirmOwnReport, "Tú reportaste el resultado: lo confirma o disputa el otro grupo.");
        }

        if (await _members.GetAsync(rivalGroup, userId, cancellationToken) is null)
        {
            throw ApiException.Forbidden(ErrorCodes.NotMatchPlayer, "Solo el grupo rival puede confirmar o disputar.");
        }

        return new Side(rivalGroup, rivalGroup == match.P1GroupId);
    }

    private static void RequireInProgress(Tournament tournament)
    {
        if (tournament.Status != "in_progress")
        {
            throw ApiException.Conflict(ErrorCodes.TournamentNotInProgress, "El torneo no está en curso.");
        }
    }

    private async Task<byte[]> ValidatePhotoAsync(byte[] content, string contentType, CancellationToken cancellationToken)
    {
        if (!AllowedContentTypes.Contains(contentType))
        {
            throw ApiException.Unprocessable(ErrorCodes.UnsupportedMediaType, "Solo se admiten JPEG, PNG o WebP.");
        }

        if (content.LongLength > _evidenceOptions.MaxImageBytes)
        {
            throw ApiException.Unprocessable(ErrorCodes.ImageTooLarge, "La imagen supera el tamaño permitido.");
        }

        var normalized = ImageProcessor.Normalize(content);
        var moderation = await _moderation.ModerateImageAsync(normalized, cancellationToken);
        if (!moderation.Approved)
        {
            throw ApiException.Unprocessable(ErrorCodes.ContentRejected, "La imagen fue rechazada por moderación.", new { labels = moderation.Labels });
        }

        return normalized;
    }

    private async Task TryDeleteAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            await _storage.DeleteAsync(key, CancellationToken.None);
        }
        catch (Exception)
        {
            // La regla de 30 días de evidence/ la borra de todos modos.
        }
    }

    private async Task<MatchResponse> ToResponseAsync(Match match, CancellationToken cancellationToken)
    {
        var url = string.IsNullOrWhiteSpace(match.ScreenshotKey)
            ? null
            : await _storage.GetReadUrlAsync(match.ScreenshotKey, TimeSpan.FromMinutes(15), cancellationToken);
        return TournamentService.ToMatch(match, url);
    }
}
