using MongoDB.Bson;
using Pactogg.Api.Common;
using Pactogg.Api.Infrastructure.Audit;
using Pactogg.Api.Infrastructure.Moderation;
using Pactogg.Api.Infrastructure.Redis;
using Pactogg.Api.Modules.Auth.Abstractions;
using Pactogg.Api.Modules.Contracts;
using Pactogg.Api.Modules.Contracts.Abstractions;
using Pactogg.Api.Modules.Contracts.Domain;
using Pactogg.Api.Modules.Groups;
using Pactogg.Api.Modules.Groups.Abstractions;
using Pactogg.Api.Modules.Groups.Domain;
using Pactogg.Api.Modules.Notifications;
using Pactogg.Api.Modules.Votes.Abstractions;
using Pactogg.Api.Modules.Votes.Domain;
using Pactogg.Api.Modules.Votes.Dtos;

namespace Pactogg.Api.Modules.Votes;

/// <summary>
/// VOT-01..05. Consenso: un lado gana cuando sus votos alcanzan el consenso del grupo (66 % por defecto)
/// sobre los jurados asignados. Si todos votan sin consenso, la votación vence o no hubo jurados,
/// decide el administrador.
/// </summary>
public sealed class VoteService
{
    private readonly IContractRepository _contracts;
    private readonly IVoteRepository _votes;
    private readonly IGroupMemberRepository _members;
    private readonly IUserRepository _users;
    private readonly GroupAccess _access;
    private readonly IModerationService _moderation;
    private readonly NotificationService _notifications;
    private readonly IAuditService _audit;
    private readonly ICacheService _cache;

    public VoteService(
        IContractRepository contracts,
        IVoteRepository votes,
        IGroupMemberRepository members,
        IUserRepository users,
        GroupAccess access,
        IModerationService moderation,
        NotificationService notifications,
        IAuditService audit,
        ICacheService cache)
    {
        _contracts = contracts;
        _votes = votes;
        _members = members;
        _users = users;
        _access = access;
        _moderation = moderation;
        _notifications = notifications;
        _audit = audit;
        _cache = cache;
    }

    /// <summary>VOT-01: retos donde soy jurado y aún no voto.</summary>
    public async Task<(IReadOnlyList<PendingVoteResponse> Items, object Meta)> ListPendingAsync(
        ObjectId userId,
        int limit,
        string? cursor,
        CancellationToken cancellationToken)
    {
        ListQuery.ValidateLimit(limit);
        var (items, hasMore) = await _contracts.ListPendingVotesAsync(userId, limit, ListQuery.ParseCursor(cursor), cancellationToken);
        var next = hasMore && items.Count > 0 ? items[^1].Id.ToString() : null;
        var responses = items.Select(x => new PendingVoteResponse
        {
            Contract = ContractService.ToResponse(x),
            VotingEndsAt = x.VotingEndsAt
        }).ToList();
        return (responses, new { next_cursor = next, has_more = hasMore });
    }

    /// <summary>VOT-02: un voto por jurado, inmutable. Puede cerrar la votación y repartir la fianza.</summary>
    public async Task<CastVoteResponse> CastAsync(ObjectId userId, string contractId, CastVoteRequest request, CancellationToken cancellationToken)
    {
        var (contract, group, _) = await LoadAsync(userId, contractId, cancellationToken);
        if (contract.Status != "voting")
        {
            throw ApiException.Conflict(ErrorCodes.InvalidContractState, "El reto no está en votación.");
        }

        if (!contract.Jurors.Contains(userId))
        {
            throw ApiException.Forbidden(ErrorCodes.NotJuror, "No eres jurado de este reto.");
        }

        if (contract.Voters.Contains(userId))
        {
            throw ApiException.Conflict(ErrorCodes.AlreadyVoted, "Ya votaste en este reto.");
        }

        var comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();
        if (comment is not null)
        {
            var moderation = await _moderation.ModerateTextAsync(comment, cancellationToken);
            if (!moderation.Approved)
            {
                throw ApiException.Unprocessable(ErrorCodes.ContentRejected, "El comentario fue rechazado por moderación.", new { labels = moderation.Labels });
            }
        }

        var now = DateTime.UtcNow;
        var legit = request.Vote == "legit";
        var updated = await _contracts.TryRegisterVoteAsync(contract.Id, userId, legit, now, cancellationToken)
            ?? throw ApiException.Conflict(ErrorCodes.AlreadyVoted, "Ya votaste o la votación cerró.");

        await _votes.CreateAsync(new Vote
        {
            Id = ObjectId.GenerateNewId(),
            ContractId = contract.Id,
            GroupId = contract.GroupId,
            JurorId = userId,
            Value = request.Vote,
            Comment = comment,
            CreatedAt = now
        }, cancellationToken);
        await _audit.AppendAsync("vote.cast", userId, "contract", contract.Id, contract.GroupId, new { contract_id = contract.Id.ToString() }, cancellationToken);

        var status = updated.Status;
        var needsAdmin = false;
        var outcome = Evaluate(updated, group.Settings.Consensus);
        if (outcome is not null)
        {
            var resolved = await ResolveAndNotifyAsync(updated, outcome, "jury", null, cancellationToken);
            status = resolved?.Status ?? status;
        }
        else if (updated.Voters.Count >= updated.Jurors.Count)
        {
            needsAdmin = true;
            await NotifyAdminsAsync(updated, cancellationToken);
        }

        await _cache.BumpVersionAsync(CacheExtensions.GroupScope(contract.GroupId), cancellationToken);
        return new CastVoteResponse
        {
            ContractId = contract.Id.ToString(),
            Vote = request.Vote,
            Status = status,
            VotesCast = updated.Voters.Count,
            JurorsAssigned = updated.Jurors.Count,
            NeedsAdmin = needsAdmin
        };
    }

    /// <summary>VOT-03: cuántos votaron y cuántos faltan; el sentido de los votos solo se revela al cerrar.</summary>
    public async Task<VoteSummaryResponse> SummaryAsync(ObjectId userId, string contractId, CancellationToken cancellationToken)
    {
        var (contract, _, _) = await LoadAsync(userId, contractId, cancellationToken);
        var closed = contract.Status is "fulfilled" or "cheating";
        return new VoteSummaryResponse
        {
            ContractId = contract.Id.ToString(),
            Status = contract.Status,
            JurorsAssigned = contract.Jurors.Count,
            VotesCast = contract.Voters.Count,
            VotesRemaining = Math.Max(0, contract.Jurors.Count - contract.Voters.Count),
            VotingEndsAt = contract.VotingEndsAt,
            IAmJuror = contract.Jurors.Contains(userId),
            IVoted = contract.Voters.Contains(userId),
            AdminCanResolve = contract.Status == "voting" && AdminMayResolve(contract, DateTime.UtcNow),
            LegitVotes = closed ? contract.LegitVotes : null,
            CheatVotes = closed ? contract.CheatVotes : null
        };
    }

    /// <summary>VOT-04: el administrador decide un empate, una votación vencida o un reto sin jurados.</summary>
    public async Task<ContractResultResponse> AdminResolveAsync(ObjectId userId, string contractId, ResolveVotingRequest request, CancellationToken cancellationToken)
    {
        var (contract, _, member) = await LoadAsync(userId, contractId, cancellationToken);
        if (member.Role != "admin")
        {
            throw ApiException.Forbidden(ErrorCodes.NotGroupAdmin, "Solo un administrador del grupo puede resolver.");
        }

        if (contract.Status != "voting")
        {
            throw ApiException.Conflict(ErrorCodes.InvalidContractState, "El reto no está en votación.");
        }

        if (!AdminMayResolve(contract, DateTime.UtcNow))
        {
            throw ApiException.Conflict(ErrorCodes.VotingNotFinished, "La votación sigue en curso; espera a que voten los jurados o a que venza.");
        }

        var resolved = await ResolveAndNotifyAsync(contract, request.Outcome, "admin", userId, cancellationToken)
            ?? throw ApiException.Conflict(ErrorCodes.InvalidContractState, "El reto cambió; recarga e intenta de nuevo.");
        await _cache.BumpVersionAsync(CacheExtensions.GroupScope(contract.GroupId), cancellationToken);
        return await BuildResultAsync(resolved, cancellationToken);
    }

    /// <summary>VOT-05: resultado final con los votos revelados.</summary>
    public async Task<ContractResultResponse> ResultAsync(ObjectId userId, string contractId, CancellationToken cancellationToken)
    {
        var (contract, _, _) = await LoadAsync(userId, contractId, cancellationToken);
        if (contract.Status is not ("fulfilled" or "cheating"))
        {
            throw ApiException.Conflict(ErrorCodes.InvalidContractState, "El reto todavía no tiene resultado.");
        }

        return await BuildResultAsync(contract, cancellationToken);
    }

    internal static string? Evaluate(Contract contract, double consensus)
    {
        var jurors = contract.Jurors.Count;
        if (jurors == 0)
        {
            return null;
        }

        if (contract.LegitVotes / (double)jurors >= consensus)
        {
            return "fulfilled";
        }

        if (contract.CheatVotes / (double)jurors >= consensus)
        {
            return "cheating";
        }

        return null;
    }

    private static bool AdminMayResolve(Contract contract, DateTime now) =>
        contract.Jurors.Count == 0
        || contract.Voters.Count >= contract.Jurors.Count
        || (contract.VotingEndsAt is DateTime ends && ends <= now);

    private async Task<Contract?> ResolveAndNotifyAsync(Contract contract, string outcome, string resolvedBy, ObjectId? actor, CancellationToken cancellationToken)
    {
        var resolved = await _contracts.ResolveAsync(contract.Id, outcome, resolvedBy, DateTime.UtcNow, cancellationToken);
        if (resolved is null)
        {
            return null;
        }

        await _audit.AppendAsync(
            "contract.resolved",
            actor,
            "contract",
            resolved.Id,
            resolved.GroupId,
            new { contract_id = resolved.Id.ToString(), outcome, resolved_by = resolvedBy },
            cancellationToken);

        var parties = new List<ObjectId> { resolved.ChallengerId };
        var executor = resolved.ExecutorId ?? resolved.AcceptedBy ?? resolved.TargetId;
        if (executor is ObjectId executorId)
        {
            parties.Add(executorId);
            await _cache.BumpVersionAsync(CacheExtensions.UserScope(executorId), cancellationToken);
        }

        await _cache.BumpVersionAsync(CacheExtensions.UserScope(resolved.ChallengerId), cancellationToken);
        var fulfilled = outcome == "fulfilled";
        await _notifications.NotifyAsync(
            parties,
            "contract_resolved",
            fulfilled ? "¡Reto cumplido!" : "Reto marcado como trampa",
            fulfilled
                ? $"{resolved.Game}: el jurado lo validó. Recompensa: {resolved.Reward}"
                : $"{resolved.Game}: el jurado detectó trampa; la fianza pasa al retador.",
            resolved.GroupId,
            resolved.Id,
            cancellationToken: cancellationToken);
        return resolved;
    }

    private async Task NotifyAdminsAsync(Contract contract, CancellationToken cancellationToken)
    {
        var (members, _) = await _members.ListByGroupAsync(contract.GroupId, null, 50, null, cancellationToken);
        await _notifications.NotifyAsync(
            members.Where(x => x.Role == "admin").Select(x => x.UserId),
            "vote_tie",
            "Votación sin consenso",
            $"{contract.Game}: los jurados no se pusieron de acuerdo. Decide como administrador.",
            contract.GroupId,
            contract.Id,
            cancellationToken: cancellationToken);
    }

    private async Task<ContractResultResponse> BuildResultAsync(Contract contract, CancellationToken cancellationToken)
    {
        var votes = await _votes.ListByContractAsync(contract.Id, cancellationToken);
        var users = (await _users.ListByIdsAsync(votes.Select(x => x.JurorId).ToArray(), cancellationToken)).ToDictionary(x => x.Id);
        var bondMovement = contract.AcceptedBy is null || contract.BondRequired == 0
            ? "none"
            : contract.Status == "fulfilled" ? "bond_release" : "bond_forfeit";

        return new ContractResultResponse
        {
            ContractId = contract.Id.ToString(),
            Outcome = contract.Status,
            ResolvedBy = contract.ResolvedBy,
            ResolvedAt = contract.ResolvedAt,
            LegitVotes = contract.LegitVotes,
            CheatVotes = contract.CheatVotes,
            BondRequired = contract.BondRequired,
            BondMovement = bondMovement,
            ChallengerId = contract.ChallengerId.ToString(),
            ExecutorId = (contract.ExecutorId ?? contract.AcceptedBy ?? contract.TargetId)?.ToString(),
            Reward = contract.Reward,
            Votes = votes.Select(vote => new RevealedVoteResponse
            {
                JurorId = vote.JurorId.ToString(),
                Nickname = users.TryGetValue(vote.JurorId, out var user) ? user.Nickname : "jurado",
                Vote = vote.Value,
                Comment = vote.Comment,
                CreatedAt = vote.CreatedAt
            }).ToList()
        };
    }

    private async Task<(Contract Contract, Group Group, GroupMember Member)> LoadAsync(ObjectId userId, string contractId, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(contractId, out var id))
        {
            throw ApiException.NotFound("El reto no existe.");
        }

        var contract = await _contracts.GetByIdAsync(id, cancellationToken)
            ?? throw ApiException.NotFound("El reto no existe.");
        var (group, member) = await _access.RequireMemberAsync(userId, contract.GroupId, cancellationToken);
        return (contract, group, member);
    }
}
