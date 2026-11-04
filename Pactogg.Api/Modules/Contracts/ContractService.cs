using MongoDB.Bson;
using Pactogg.Api.Common;
using Pactogg.Api.Infrastructure.Audit;
using Pactogg.Api.Infrastructure.Moderation;
using Pactogg.Api.Infrastructure.Redis;
using Pactogg.Api.Modules.Contracts.Abstractions;
using Pactogg.Api.Modules.Contracts.Domain;
using Pactogg.Api.Modules.Contracts.Dtos;
using Pactogg.Api.Modules.Groups.Abstractions;

namespace Pactogg.Api.Modules.Contracts;

public sealed class ContractService
{
    private readonly IContractRepository _contracts;
    private readonly IGroupRepository _groups;
    private readonly IGroupMemberRepository _members;
    private readonly IModerationService _moderation;
    private readonly IAuditService _audit;
    private readonly ICacheService _cache;

    public ContractService(
        IContractRepository contracts,
        IGroupRepository groups,
        IGroupMemberRepository members,
        IModerationService moderation,
        IAuditService audit,
        ICacheService cache)
    {
        _cache = cache;
        _contracts = contracts;
        _groups = groups;
        _members = members;
        _moderation = moderation;
        _audit = audit;
    }

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

        if (!string.Equals(request.Source, "manual", StringComparison.OrdinalIgnoreCase))
        {
            throw ApiException.Unprocessable(ErrorCodes.ValidationError, "En esta entrega solo se admite source=manual.");
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

            targetId = parsedTarget;
        }
        else if (targetType != "group")
        {
            throw ApiException.Unprocessable(ErrorCodes.InvalidTarget, "target_type debe ser group o member.");
        }

        var reward = ContractRules.NormalizeReward(request.Reward);
        var conditionModeration = await _moderation.ModerateTextAsync(request.Condition, cancellationToken);
        var rewardModeration = await _moderation.ModerateTextAsync(reward, cancellationToken);
        if (!conditionModeration.Approved || !rewardModeration.Approved)
        {
            var labels = conditionModeration.Labels.Concat(rewardModeration.Labels).Distinct().ToArray();
            throw ApiException.Unprocessable(ErrorCodes.ContentRejected, "El contrato fue rechazado por moderación.", new { labels });
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
            Source = "manual",
            EvidenceCount = 0,
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _contracts.CreateAsync(contract, cancellationToken);
        await _audit.AppendAsync("contract.created", userId, "contract", contract.Id, groupId, new { contract_id = contract.Id.ToString() }, cancellationToken);

        await _cache.BumpVersionAsync(CacheExtensions.GroupScope(groupId), cancellationToken);
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

    private static ContractResponse ToResponse(Contract contract, int? myPoints = null) => new()
    {
        Id = contract.Id.ToString(),
        GroupId = contract.GroupId.ToString(),
        ChallengerId = contract.ChallengerId.ToString(),
        TargetType = contract.TargetType,
        TargetId = contract.TargetId?.ToString(),
        Game = contract.Game,
        Condition = contract.Condition,
        Reward = contract.Reward,
        Deadline = contract.Deadline,
        BondRequired = contract.BondRequired,
        Status = contract.Status,
        AcceptedBy = contract.AcceptedBy?.ToString(),
        AcceptedAt = contract.AcceptedAt,
        ClosedReason = contract.ClosedReason,
        EvidenceCount = contract.EvidenceCount,
        Version = contract.Version,
        CreatedAt = contract.CreatedAt,
        UpdatedAt = contract.UpdatedAt,
        MyPoints = myPoints
    };
}
