using MongoDB.Bson;
using Pactogg.Api.Common;
using Pactogg.Api.Infrastructure.Audit;
using Pactogg.Api.Infrastructure.Moderation;
using Pactogg.Api.Infrastructure.Redis;
using Pactogg.Api.Modules.Auth.Abstractions;
using Pactogg.Api.Modules.Contracts;
using Pactogg.Api.Modules.Contracts.Abstractions;
using Pactogg.Api.Modules.Contracts.Domain;
using Pactogg.Api.Modules.Contracts.Lifecycle;
using Pactogg.Api.Modules.Groups;
using Pactogg.Api.Modules.Groups.Abstractions;
using Pactogg.Api.Modules.Groups.Domain;
using Pactogg.Api.Modules.Notifications;
using Pactogg.Api.Modules.Votes.Abstractions;
using Pactogg.Api.Modules.Votes.Domain;
using Pactogg.Api.Modules.Votes.Dtos;

namespace Pactogg.Api.Modules.Votes;

/// <summary>
/// VOT-01..05. Consenso (CLARIFICACION #79–84): 66 % de los votos emitidos con quórum de la mitad de los
/// jurados asignados. Al vencer sin quórum se extiende 24 h una vez; en empate decide un administrador que
/// no sea parte (o un jurado adicional una sola vez); si el administrador no decide en 48 h, se cancela
/// con devolución de fianza. Los plazos los cierra <see cref="ContractLifecycleService"/>.
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
    private readonly ContractLifecycleService _lifecycle;

    public VoteService(
        IContractRepository contracts,
        IVoteRepository votes,
        IGroupMemberRepository members,
        IUserRepository users,
        GroupAccess access,
        IModerationService moderation,
        NotificationService notifications,
        IAuditService audit,
        ICacheService cache,
        ContractLifecycleService lifecycle)
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
        _lifecycle = lifecycle;
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
        contract = await _lifecycle.ProcessVotingAsync(contract, DateTime.UtcNow, cancellationToken);
        if (contract.Status != "voting")
        {
            throw ApiException.Conflict(ErrorCodes.InvalidContractState, "El reto no está en votación.");
        }

        if (contract.AdminDeadlineAt is not null)
        {
            throw ApiException.Conflict(ErrorCodes.VotingClosed, "La votación del jurado cerró; ahora decide el administrador.");
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
        var decision = VotingRules.Decide(updated.LegitVotes, updated.CheatVotes, updated.Jurors.Count, group.Settings.Consensus, final: false);
        if (decision is VotingDecision.Fulfilled or VotingDecision.Cheating)
        {
            var resolved = await _lifecycle.ResolveAsync(updated, decision == VotingDecision.Fulfilled ? "fulfilled" : "cheating", "jury", null, cancellationToken);
            status = resolved?.Status ?? status;
        }
        else if (decision == VotingDecision.Tie)
        {
            var afterTie = await _lifecycle.HandleTieAsync(updated, now, cancellationToken);
            needsAdmin = afterTie.AdminDeadlineAt is not null;
            updated = afterTie;
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
        var (contract, _, member) = await LoadAsync(userId, contractId, cancellationToken);
        contract = await _lifecycle.ProcessVotingAsync(contract, DateTime.UtcNow, cancellationToken);
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
            AdminCanResolve = member.Role == "admin" && AdminMayResolve(contract, userId, DateTime.UtcNow),
            VotingExtended = contract.VotingExtended,
            AdminDeadlineAt = contract.AdminDeadlineAt,
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

        if (ContractLifecycleService.Parties(contract).Contains(userId))
        {
            throw ApiException.Forbidden(ErrorCodes.AdminIsParty, "Eres parte del reto; debe decidir otro administrador.");
        }

        contract = await _lifecycle.ProcessVotingAsync(contract, DateTime.UtcNow, cancellationToken);
        if (contract.Status != "voting")
        {
            throw ApiException.Conflict(ErrorCodes.InvalidContractState, "El reto no está en votación.");
        }

        if (!AdminMayResolve(contract, userId, DateTime.UtcNow))
        {
            throw ApiException.Conflict(ErrorCodes.VotingNotFinished, "La votación sigue en curso; el administrador decide solo en empate o si vence sin quórum.");
        }

        var resolved = await _lifecycle.ResolveAsync(contract, request.Outcome, "admin", userId, cancellationToken)
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

    private static bool AdminMayResolve(Contract contract, ObjectId adminId, DateTime now) =>
        contract.Status == "voting"
        && !ContractLifecycleService.Parties(contract).Contains(adminId)
        && contract.AdminDeadlineAt is DateTime deadline
        && deadline > now;

    private async Task<ContractResultResponse> BuildResultAsync(Contract contract, CancellationToken cancellationToken)
    {
        var votes = await _votes.ListByContractAsync(contract.Id, cancellationToken);
        var bondMovement = contract.AcceptedBy is null || contract.BondRequired == 0
            ? "none"
            : contract.Status == "fulfilled" ? "bond_release_and_reward" : "bond_forfeit";

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
            // Se ordena por sentido y texto (no por fecha) para no delatar el orden de emisión.
            Votes = votes.OrderBy(vote => vote.Value, StringComparer.Ordinal).ThenBy(vote => vote.Comment, StringComparer.Ordinal).Select(vote => new RevealedVoteResponse
            {
                Vote = vote.Value,
                Comment = vote.Comment,
                CreatedAt = vote.CreatedAt.Date
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
