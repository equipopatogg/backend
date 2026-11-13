using MongoDB.Bson;
using Pactogg.Api.Common;
using Pactogg.Api.Infrastructure.Audit;
using Pactogg.Api.Infrastructure.Moderation;
using Pactogg.Api.Infrastructure.Redis;
using Pactogg.Api.Modules.Contracts.Abstractions;
using Pactogg.Api.Modules.Contracts.Domain;
using Pactogg.Api.Modules.Contracts.Dtos;
using Pactogg.Api.Modules.Groups.Abstractions;
using Pactogg.Api.Modules.Notifications;

namespace Pactogg.Api.Modules.Contracts;

public sealed class ContractService
{
    private readonly IContractRepository _contracts;
    private readonly IGroupRepository _groups;
    private readonly IGroupMemberRepository _members;
    private readonly IModerationService _moderation;
    private readonly Pactogg.Api.Modules.Reports.IBlockRepository _blocks;
    private readonly IAuditService _audit;
    private readonly ICacheService _cache;
    private readonly NotificationService _notifications;

    public ContractService(
        IContractRepository contracts,
        IGroupRepository groups,
        IGroupMemberRepository members,
        IModerationService moderation,
        IAuditService audit,
        ICacheService cache,
        NotificationService notifications,
        Pactogg.Api.Modules.Reports.IBlockRepository blocks)
    {
        _cache = cache;
        _notifications = notifications;
        _contracts = contracts;
        _groups = groups;
        _members = members;
        _moderation = moderation;
        _blocks = blocks;
        _audit = audit;
    }

    public const int MinMembersForContracts = 3;

    public async Task<ContractResponse> CreateAsync(ObjectId userId, CreateContractRequest request, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(request.GroupId, out var groupId))
        {
            throw ApiException.Unprocessable(ErrorCodes.ValidationError, "group_id no es válido.");
        }

        var group = await _groups.GetByIdAsync(groupId, cancellationToken)
            ?? throw ApiException.NotFound("El grupo no existe.");

        if (await _members.GetAsync(groupId, userId, cancellationToken) is null)
        {
            throw ApiException.Forbidden(ErrorCodes.NotGroupMember, "No perteneces a este grupo.");
        }

        // CLARIFICACION #22 y #75: hace falta al menos 3 miembros (retador, retado y un jurado).
        if (group.MemberCount < MinMembersForContracts)
        {
            throw ApiException.Conflict(ErrorCodes.GroupTooSmall, $"El grupo necesita al menos {MinMembersForContracts} miembros para lanzar retos.");
        }

        // 005-R14: source=voice guarda la frase dictada; source=manual no lleva frase.
        var source = string.IsNullOrWhiteSpace(request.Source) ? "manual" : request.Source.Trim().ToLowerInvariant();
        var originalPhrase = string.IsNullOrWhiteSpace(request.OriginalPhrase) ? null : request.OriginalPhrase.Trim();
        if (source is not ("manual" or "voice"))
        {
            throw ApiException.Unprocessable(ErrorCodes.ValidationError, "source debe ser manual o voice.");
        }

        if (source == "manual" && originalPhrase is not null)
        {
            throw ApiException.Unprocessable(ErrorCodes.ValidationError, "original_phrase solo va con source=voice.");
        }

        if (originalPhrase is { Length: > 300 })
        {
            throw ApiException.Unprocessable(ErrorCodes.ValidationError, "original_phrase admite hasta 300 caracteres.");
        }

        var deadline = request.Deadline.UtcDateTime;
        if (deadline <= DateTime.UtcNow)
        {
            throw ApiException.Unprocessable(ErrorCodes.ValidationError, "El deadline debe ser futuro.");
        }

        if (!group.Settings.BondOptions.Contains(request.BondRequired))
        {
            throw ApiException.Unprocessable(ErrorCodes.InvalidBond, "La fianza no está permitida por el grupo.");
        }

        var targetType = string.IsNullOrWhiteSpace(request.TargetType) ? "group" : request.TargetType.Trim().ToLowerInvariant();
        ObjectId? targetId = null;
        if (targetType == "member")
        {
            if (!ObjectId.TryParse(request.TargetId, out var parsedTarget)
                || await _members.GetAsync(groupId, parsedTarget, cancellationToken) is null)
            {
                throw ApiException.Unprocessable(ErrorCodes.InvalidTarget, "El miembro retado no pertenece al grupo.");
            }

            if (parsedTarget == userId)
            {
                throw ApiException.Unprocessable(ErrorCodes.InvalidTarget, "No puedes retarte a ti mismo.");
            }

            // REP-D2: quien te bloqueó no recibe retos directos tuyos.
            if (await _blocks.IsBlockedAsync(parsedTarget, userId, cancellationToken))
            {
                throw ApiException.Forbidden(ErrorCodes.BlockedByUser, "Esta persona no acepta retos tuyos.");
            }

            targetId = parsedTarget;
        }
        else if (targetType != "group")
        {
            throw ApiException.Unprocessable(ErrorCodes.InvalidTarget, "target_type debe ser group o member.");
        }

        var reward = ContractRules.NormalizeReward(request.Reward);
        // 004-R4: juego, condición y recompensa son texto libre y pasan por moderación (constitución §3.9).
        var gameModeration = await _moderation.ModerateTextAsync(request.Game.Trim(), cancellationToken);
        var conditionModeration = await _moderation.ModerateTextAsync(request.Condition, cancellationToken);
        var rewardModeration = await _moderation.ModerateTextAsync(reward, cancellationToken);
        if (!gameModeration.Approved || !conditionModeration.Approved || !rewardModeration.Approved)
        {
            var labels = gameModeration.Labels.Concat(conditionModeration.Labels).Concat(rewardModeration.Labels).Distinct().ToArray();
            throw ApiException.Unprocessable(ErrorCodes.ContentRejected, "El contrato fue rechazado por moderación.", new { labels });
        }

        // La frase dictada también la ven los miembros del grupo: pasa por la misma moderación.
        if (source == "voice" && originalPhrase is not null && await _moderation.ModerateTextAsync(originalPhrase, cancellationToken) is { Approved: false } phraseModeration)
        {
            throw ApiException.Unprocessable(ErrorCodes.ContentRejected, "La frase dictada fue rechazada por moderación.", new { labels = phraseModeration.Labels });
        }

        var now = DateTime.UtcNow;
        var contract = new Contract
        {
            Id = ObjectId.GenerateNewId(),
            GroupId = groupId,
            ChallengerId = userId,
            TargetType = targetType,
            TargetId = targetId,
            Game = request.Game.Trim(),
            Condition = request.Condition.Trim(),
            Reward = reward,
            Deadline = deadline,
            BondRequired = request.BondRequired,
            Status = "open",
            Source = source,
            OriginalPhrase = source == "voice" ? originalPhrase : null,
            EvidenceCount = 0,
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _contracts.CreateAsync(contract, cancellationToken);
        await _audit.AppendAsync("contract.created", userId, "contract", contract.Id, groupId, new { contract_id = contract.Id.ToString() }, cancellationToken);

        await _cache.BumpVersionAsync(CacheExtensions.GroupScope(groupId), cancellationToken);
        await NotifyCreatedAsync(group.Name, contract, cancellationToken);
        return ToResponse(contract);
    }

    private static readonly HashSet<string> ValidStatuses = new(StringComparer.Ordinal)
    {
        "open",
        "in_progress",
        "voting",
        "fulfilled",
        "cheating",
        "expired",
        "cancelled"
    };

    /// <summary>CON-02: tablero de retos del grupo (status, mine, game, q, cursor). Caché 30 s por versión del grupo.</summary>
    public async Task<(IReadOnlyList<ContractResponse> Items, object Meta)> ListAsync(
        ObjectId userId,
        string gid,
        string? status,
        bool mine,
        string? game,
        string? q,
        int limit,
        string? cursor,
        CancellationToken cancellationToken)
    {
        ListQuery.ValidateLimit(limit);
        var cursorId = ListQuery.ParseCursor(cursor);
        var statusFilter = string.IsNullOrWhiteSpace(status) ? null : status.Trim().ToLowerInvariant();
        if (statusFilter is not null && !ValidStatuses.Contains(statusFilter))
        {
            throw ApiException.Unprocessable(ErrorCodes.ValidationError, "status no es válido.");
        }

        var gameFilter = ListQuery.NormalizeText(game, 60, "game");
        var query = ListQuery.NormalizeText(q, 60, "q");
        var groupId = await RequireMemberAsync(userId, gid, cancellationToken);

        var version = await _cache.GetVersionAsync(CacheExtensions.GroupScope(groupId), cancellationToken);
        var mineKey = mine ? userId.ToString() : "all";
        var snapshot = await _cache.GetOrCreateJsonAsync(
            $"contracts:list:{groupId}:{version}:{statusFilter}:{mineKey}:{gameFilter}:{query}:{limit}:{cursor}",
            TimeSpan.FromSeconds(30),
            async () =>
            {
                var filter = new ContractListFilter(statusFilter, mine ? userId : null, gameFilter, query);
                var (items, hasMore) = await _contracts.ListForGroupAsync(groupId, filter, limit, cursorId, cancellationToken);
                var next = hasMore && items.Count > 0 ? items[^1].Id.ToString() : null;
                return new PageSnapshot<ContractResponse>(items.Select(x => ToResponse(x)).ToList(), next, hasMore);
            },
            cancellationToken);

        return (snapshot.Items, snapshot.Meta);
    }

    /// <summary>CON-03: detalle de un reto; solo miembros de su grupo.</summary>
    public async Task<ContractResponse> GetAsync(ObjectId userId, string id, CancellationToken cancellationToken)
    {
        var contract = await RequireContractForMemberAsync(userId, id, cancellationToken);
        return ToResponse(contract);
    }

    /// <summary>
    /// CON-06: el retado (o cualquier miembro si el reto es para el grupo) acepta y se le retiene la fianza.
    /// Saldo insuficiente → 402; nunca queda negativo. No se puede aceptar el propio reto.
    /// </summary>
    public async Task<ContractResponse> AcceptAsync(ObjectId userId, string id, ContractActionRequest? request, CancellationToken cancellationToken)
    {
        var contract = await RequireContractForMemberAsync(userId, id, cancellationToken);
        if (contract.ChallengerId == userId)
        {
            throw ApiException.Conflict(ErrorCodes.CannotAcceptOwnContract, "No puedes aceptar tu propio reto.");
        }

        if (contract.TargetType == "member" && contract.TargetId != userId)
        {
            throw ApiException.Forbidden(ErrorCodes.NotContractTarget, "Este reto está dirigido a otro miembro.");
        }

        EnsureOpen(contract, request);
        var now = DateTime.UtcNow;
        if (contract.Deadline <= now)
        {
            throw ApiException.Conflict(ErrorCodes.InvalidContractState, "El reto ya venció.");
        }

        var result = await _contracts.AcceptWithBondAsync(contract.Id, contract.Version, userId, now, cancellationToken);
        switch (result.Outcome)
        {
            case AcceptContractOutcome.InsufficientPoints:
                throw ApiException.PaymentRequired(
                    ErrorCodes.InsufficientPoints,
                    $"No tienes puntos suficientes para la fianza de {contract.BondRequired}.");
            case AcceptContractOutcome.StateChanged:
                throw ApiException.Conflict(ErrorCodes.InvalidContractState, "El reto cambió; recarga e intenta de nuevo.");
        }

        var accepted = result.Contract!;
        await _audit.AppendAsync(
            "contract.accepted",
            userId,
            "contract",
            accepted.Id,
            accepted.GroupId,
            new { contract_id = accepted.Id.ToString(), bond = accepted.BondRequired, balance_after = result.BalanceAfter },
            cancellationToken);
        await _cache.BumpVersionAsync(CacheExtensions.GroupScope(accepted.GroupId), cancellationToken);
        await _cache.BumpVersionAsync(CacheExtensions.UserScope(userId), cancellationToken);
        await _notifications.NotifyAsync(
            [accepted.ChallengerId],
            "contract_accepted",
            "Aceptaron tu reto",
            $"{accepted.Game}: {accepted.Condition}",
            accepted.GroupId,
            accepted.Id,
            cancellationToken: cancellationToken);

        return ToResponse(accepted, result.BalanceAfter);
    }

    /// <summary>CON-07: el retado rechaza un reto open dirigido a él; pasa a cancelled (closed_reason = rejected).</summary>
    public async Task<ContractResponse> RejectAsync(ObjectId userId, string id, ContractActionRequest? request, CancellationToken cancellationToken)
    {
        var contract = await RequireContractForMemberAsync(userId, id, cancellationToken);
        if (contract.TargetType != "member" || contract.TargetId != userId)
        {
            throw ApiException.Forbidden(ErrorCodes.NotContractTarget, "Solo el retado puede rechazar este reto.");
        }

        EnsureOpen(contract, request);
        var rejected = await _contracts.TryRejectAsync(contract.Id, contract.Version, userId, DateTime.UtcNow, cancellationToken)
            ?? throw ApiException.Conflict(ErrorCodes.InvalidContractState, "El reto cambió; recarga e intenta de nuevo.");

        await _audit.AppendAsync(
            "contract.rejected",
            userId,
            "contract",
            rejected.Id,
            rejected.GroupId,
            new { contract_id = rejected.Id.ToString() },
            cancellationToken);
        await _cache.BumpVersionAsync(CacheExtensions.GroupScope(rejected.GroupId), cancellationToken);

        return ToResponse(rejected);
    }

    /// <summary>CON-04: el retador edita su reto mientras siga abierto y nadie lo haya aceptado.</summary>
    public async Task<ContractResponse> UpdateAsync(ObjectId userId, string id, UpdateContractRequest request, CancellationToken cancellationToken)
    {
        var contract = await RequireContractForMemberAsync(userId, id, cancellationToken);
        RequireChallenger(contract, userId);
        EnsureOpen(contract, new ContractActionRequest { Version = request.Version });
        if (contract.AcceptedBy is not null)
        {
            throw ApiException.Conflict(ErrorCodes.InvalidContractState, "El reto ya fue aceptado.");
        }

        var group = await _groups.GetByIdAsync(contract.GroupId, cancellationToken) ?? throw ApiException.NotFound("El grupo no existe.");
        var game = request.Game?.Trim() ?? contract.Game;
        var condition = request.Condition?.Trim() ?? contract.Condition;
        var reward = request.Reward is null ? contract.Reward : ContractRules.NormalizeReward(request.Reward);
        var deadline = request.Deadline?.UtcDateTime ?? contract.Deadline;
        var bond = request.BondRequired ?? contract.BondRequired;

        if (deadline <= DateTime.UtcNow)
        {
            throw ApiException.Unprocessable(ErrorCodes.ValidationError, "El deadline debe ser futuro.");
        }

        if (bond != contract.BondRequired && !group.Settings.BondOptions.Contains(bond))
        {
            throw ApiException.Unprocessable(ErrorCodes.InvalidBond, "La fianza no está permitida por el grupo.");
        }

        if (request.Game is not null || request.Condition is not null || request.Reward is not null)
        {
            var gameModeration = await _moderation.ModerateTextAsync(game, cancellationToken);
            var conditionModeration = await _moderation.ModerateTextAsync(condition, cancellationToken);
            var rewardModeration = await _moderation.ModerateTextAsync(reward, cancellationToken);
            if (!gameModeration.Approved || !conditionModeration.Approved || !rewardModeration.Approved)
            {
                var labels = gameModeration.Labels.Concat(conditionModeration.Labels).Concat(rewardModeration.Labels).Distinct().ToArray();
                throw ApiException.Unprocessable(ErrorCodes.ContentRejected, "El contrato fue rechazado por moderación.", new { labels });
            }
        }

        var updated = await _contracts.TryUpdateOpenAsync(contract.Id, contract.Version, game, condition, reward, deadline, bond, DateTime.UtcNow, cancellationToken)
            ?? throw ApiException.Conflict(ErrorCodes.InvalidContractState, "El reto cambió; recarga e intenta de nuevo.");
        await _audit.AppendAsync("contract.updated", userId, "contract", updated.Id, updated.GroupId, new { contract_id = updated.Id.ToString(), version = updated.Version }, cancellationToken);
        await _cache.BumpVersionAsync(CacheExtensions.GroupScope(updated.GroupId), cancellationToken);
        return ToResponse(updated);
    }

    /// <summary>CON-05: el retador cancela un reto que nadie ha aceptado.</summary>
    public async Task CancelAsync(ObjectId userId, string id, int? version, CancellationToken cancellationToken)
    {
        var contract = await RequireContractForMemberAsync(userId, id, cancellationToken);
        RequireChallenger(contract, userId);
        EnsureOpen(contract, new ContractActionRequest { Version = version });
        var cancelled = await _contracts.TryCancelOpenAsync(contract.Id, contract.Version, DateTime.UtcNow, cancellationToken)
            ?? throw ApiException.Conflict(ErrorCodes.InvalidContractState, "El reto ya fue aceptado o cambió; recarga e intenta de nuevo.");
        await _audit.AppendAsync("contract.cancelled", userId, "contract", cancelled.Id, cancelled.GroupId, new { contract_id = cancelled.Id.ToString() }, cancellationToken);
        await _cache.BumpVersionAsync(CacheExtensions.GroupScope(cancelled.GroupId), cancellationToken);
    }

    /// <summary>
    /// CON-09 (spec 004-R16, Q-022): un administrador del grupo cancela un reto en open o in_progress, por ejemplo
    /// uno imposible. La fianza vuelve completa a quien aceptó y no cambian rachas ni Índice de Palabra.
    /// </summary>
    public async Task<ContractResponse> CancelByAdminAsync(ObjectId userId, string id, AdminCancelContractRequest request, CancellationToken cancellationToken)
    {
        var contract = await RequireContractForMemberAsync(userId, id, cancellationToken);
        var member = await _members.GetAsync(contract.GroupId, userId, cancellationToken);
        if (member?.Role != "admin")
        {
            throw ApiException.Forbidden(ErrorCodes.NotGroupAdmin, "Solo un administrador del grupo puede cancelar retos de otros.");
        }

        if (contract.AcceptedBy == userId)
        {
            throw ApiException.Conflict(ErrorCodes.AdminIsParticipant, "Aceptaste este reto: otro administrador tiene que cancelarlo.");
        }

        if (contract.Status is not ("open" or "in_progress"))
        {
            throw ApiException.Conflict(ErrorCodes.InvalidContractState, contract.Status == "voting"
                ? "El reto ya tiene evidencia y está en votación; lo decide el jurado."
                : $"El reto está en estado {contract.Status}.");
        }

        var reason = request.Reason.Trim();
        if (reason.Length is < 5 or > 140)
        {
            throw ApiException.Unprocessable(ErrorCodes.ValidationError, "El motivo debe tener de 5 a 140 caracteres.");
        }

        var moderation = await _moderation.ModerateTextAsync(reason, cancellationToken);
        if (!moderation.Approved)
        {
            throw ApiException.Unprocessable(ErrorCodes.ContentRejected, "El motivo fue rechazado por moderación.", new { labels = moderation.Labels });
        }

        var cancelled = await _contracts.TryCancelByAdminAsync(contract.Id, DateTime.UtcNow, cancellationToken)
            ?? throw ApiException.Conflict(ErrorCodes.InvalidContractState, "El reto cambió de estado; recarga e intenta de nuevo.");
        await _audit.AppendAsync("contract.cancelled_by_admin", userId, "contract", cancelled.Id, cancelled.GroupId,
            new { contract_id = cancelled.Id.ToString(), reason, bond_returned = cancelled.AcceptedBy is null ? 0 : cancelled.BondRequired }, cancellationToken);
        await _cache.BumpVersionAsync(CacheExtensions.GroupScope(cancelled.GroupId), cancellationToken);

        var recipients = new[] { cancelled.ChallengerId, cancelled.AcceptedBy ?? cancelled.ChallengerId }
            .Distinct()
            .Where(x => x != userId)
            .ToArray();
        if (recipients.Length > 0)
        {
            await _notifications.NotifyAsync(
                recipients,
                "contract_cancelled",
                "Un administrador canceló un reto",
                cancelled.AcceptedBy is null
                    ? $"{cancelled.Game}: {reason}"
                    : $"{cancelled.Game}: {reason}. Se devolvió la fianza de {cancelled.BondRequired} pts.",
                cancelled.GroupId,
                cancelled.Id,
                cancellationToken: cancellationToken);
        }

        return ToResponse(cancelled);
    }

    private static void RequireChallenger(Contract contract, ObjectId userId)
    {
        if (contract.ChallengerId != userId)
        {
            throw ApiException.Forbidden(ErrorCodes.NotContractOwner, "Solo quien publicó el reto puede cambiarlo.");
        }
    }

    private static void EnsureOpen(Contract contract, ContractActionRequest? request)
    {
        if (contract.Status != "open")
        {
            throw ApiException.Conflict(ErrorCodes.InvalidContractState, $"El reto está en estado {contract.Status}.");
        }

        if (request?.Version is int version && version != contract.Version)
        {
            throw ApiException.Conflict(ErrorCodes.InvalidContractState, "El reto cambió; recarga e intenta de nuevo.");
        }
    }

    private async Task<ObjectId> RequireMemberAsync(ObjectId userId, string gid, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(gid, out var groupId) || await _groups.GetByIdAsync(groupId, cancellationToken) is null)
        {
            throw ApiException.NotFound("El grupo no existe.");
        }

        if (await _members.GetAsync(groupId, userId, cancellationToken) is null)
        {
            throw ApiException.Forbidden(ErrorCodes.NotGroupMember, "No perteneces a este grupo.");
        }

        return groupId;
    }

    private async Task<Contract> RequireContractForMemberAsync(ObjectId userId, string id, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(id, out var contractId))
        {
            throw ApiException.NotFound("El reto no existe.");
        }

        var contract = await _contracts.GetByIdAsync(contractId, cancellationToken)
            ?? throw ApiException.NotFound("El reto no existe.");

        if (await _members.GetAsync(contract.GroupId, userId, cancellationToken) is null)
        {
            throw ApiException.Forbidden(ErrorCodes.NotGroupMember, "No perteneces al grupo de este reto.");
        }

        return contract;
    }

    /// <summary>ID 69: aviso de reto recibido (al retado, o a todo el grupo menos el retador).</summary>
    private async Task NotifyCreatedAsync(string groupName, Contract contract, CancellationToken cancellationToken)
    {
        IEnumerable<ObjectId> recipients;
        string title;
        if (contract.TargetType == "member" && contract.TargetId is ObjectId target)
        {
            recipients = [target];
            title = "Te retaron";
        }
        else
        {
            var (members, _) = await _members.ListByGroupAsync(contract.GroupId, null, 50, null, cancellationToken);
            recipients = members.Select(x => x.UserId).Where(x => x != contract.ChallengerId);
            title = $"Nuevo reto en {groupName}";
        }

        await _notifications.NotifyAsync(
            recipients,
            "contract_received",
            title,
            $"{contract.Game}: {contract.Condition} · Fianza {contract.BondRequired} pts",
            contract.GroupId,
            contract.Id,
            cancellationToken: cancellationToken);
    }

    internal static ContractResponse ToResponse(Contract contract, int? myPoints = null) => new()
    {
        Id = contract.Id.ToString(),
        GroupId = contract.GroupId.ToString(),
        ChallengerId = contract.ChallengerId.ToString(),
        TargetType = contract.TargetType,
        TargetId = contract.TargetId?.ToString(),
        Game = contract.Game,
        Condition = contract.Condition,
        Reward = contract.Reward,
        Source = contract.Source,
        OriginalPhrase = contract.OriginalPhrase,
        Deadline = contract.Deadline,
        BondRequired = contract.BondRequired,
        Status = contract.Status,
        AcceptedBy = contract.AcceptedBy?.ToString(),
        AcceptedAt = contract.AcceptedAt,
        ClosedReason = contract.ClosedReason,
        ExecutorId = contract.ExecutorId?.ToString(),
        JurorsCount = contract.Jurors.Count,
        VotingEndsAt = contract.VotingEndsAt,
        ResolvedBy = contract.ResolvedBy,
        ResolvedAt = contract.ResolvedAt,
        EvidenceCount = contract.EvidenceCount,
        Version = contract.Version,
        CreatedAt = contract.CreatedAt,
        UpdatedAt = contract.UpdatedAt,
        MyPoints = myPoints
    };
}
