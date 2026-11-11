using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using Pactogg.Api.Infrastructure.Audit;
using Pactogg.Api.Infrastructure.Redis;
using Pactogg.Api.Modules.Contracts.Abstractions;
using Pactogg.Api.Modules.Contracts.Domain;
using Pactogg.Api.Modules.Groups.Abstractions;
using Pactogg.Api.Modules.Notifications;
using Pactogg.Api.Modules.Reputation;

namespace Pactogg.Api.Modules.Contracts.Lifecycle;

/// <summary>
/// Transiciones automáticas del reto: vencimiento sin evidencia (CLAR #36), cierre de votación,
/// extensión única de 24 h, fase del administrador (48 h), empate y jurado adicional (CLAR #79–84).
/// Todas las escrituras son condicionales, así que dos instancias pueden correrlo a la vez sin duplicar efectos.
/// </summary>
public sealed class ContractLifecycleService
{
    private readonly IContractRepository _contracts;
    private readonly IGroupRepository _groups;
    private readonly IGroupMemberRepository _members;
    private readonly NotificationService _notifications;
    private readonly IAuditService _audit;
    private readonly ICacheService _cache;
    private readonly ContractLifecycleOptions _options;
    private readonly ReputationService _reputation;

    public ContractLifecycleService(
        IContractRepository contracts,
        IGroupRepository groups,
        IGroupMemberRepository members,
        NotificationService notifications,
        IAuditService audit,
        ICacheService cache,
        IOptions<ContractLifecycleOptions> options,
        ReputationService reputation)
    {
        _reputation = reputation;
        _contracts = contracts;
        _groups = groups;
        _members = members;
        _notifications = notifications;
        _audit = audit;
        _cache = cache;
        _options = options.Value;
    }

    public ContractLifecycleOptions Options => _options;

    /// <summary>Una pasada de la tarea programada. Devuelve cuántos retos se revisaron.</summary>
    public async Task<int> RunOnceAsync(DateTime now, CancellationToken cancellationToken)
    {
        var processed = 0;
        foreach (var contract in await _contracts.ListPastDeadlineAsync(now, _options.BatchSize, cancellationToken))
        {
            await ExpireAsync(contract, now, cancellationToken);
            processed++;
        }

        var reminder = now.AddHours(_options.ReminderHoursBefore);
        foreach (var contract in await _contracts.ListVotingDueAsync(now, reminder, _options.BatchSize, cancellationToken))
        {
            await ProcessVotingAsync(contract, now, cancellationToken);
            processed++;
        }

        return processed;
    }

    /// <summary>Procesa un reto en voting si algún plazo venció. Devuelve el reto actualizado.</summary>
    public async Task<Contract> ProcessVotingAsync(Contract contract, DateTime now, CancellationToken cancellationToken)
    {
        if (contract.Status != "voting")
        {
            return contract;
        }

        if (contract.AdminDeadlineAt is DateTime adminDeadline)
        {
            if (adminDeadline <= now)
            {
                var cancelled = await _contracts.TryCancelVotingWithRefundAsync(contract.Id, "admin_timeout", now, cancellationToken);
                if (cancelled is not null)
                {
                    await AfterCloseAsync(cancelled, "contract.cancelled", "contract_cancelled", "Reto cancelado",
                        $"{cancelled.Game}: nadie decidió a tiempo. Se devolvió la fianza y la reputación no cambia.", cancellationToken);
                    return cancelled;
                }
            }

            return contract;
        }

        if (contract.VotingEndsAt is not DateTime ends)
        {
            return contract;
        }

        if (ends > now)
        {
            if (ends <= now.AddHours(_options.ReminderHoursBefore) && !contract.VoteReminderSent)
            {
                await _contracts.MarkReminderSentAsync(contract.Id, cancellationToken);
                await _notifications.NotifyAsync(
                    PendingJurors(contract),
                    "vote_expiring",
                    "Votación por vencer",
                    $"{contract.Game}: te quedan pocas horas para votar.",
                    contract.GroupId,
                    contract.Id,
                    cancellationToken: cancellationToken,
                    dedupeSuffix: contract.VotingExtended ? "2" : "1");
                contract.VoteReminderSent = true;
            }

            return contract;
        }

        var group = await _groups.GetByIdAsync(contract.GroupId, cancellationToken);
        var decision = VotingRules.Decide(contract.LegitVotes, contract.CheatVotes, contract.Jurors.Count, group?.Settings.Consensus ?? 0.66, final: true);
        switch (decision)
        {
            case VotingDecision.Fulfilled:
            case VotingDecision.Cheating:
                return await ResolveAsync(contract, decision == VotingDecision.Fulfilled ? "fulfilled" : "cheating", "jury", null, cancellationToken) ?? contract;

            case VotingDecision.NoQuorum when !contract.VotingExtended && contract.Jurors.Count > 0:
                var extended = await _contracts.TryExtendVotingAsync(contract.Id, now.AddHours(_options.ExtensionHours), now, cancellationToken);
                if (extended is null)
                {
                    return contract;
                }

                await _notifications.NotifyAsync(
                    PendingJurors(extended),
                    "vote_extended",
                    "Votación extendida 24 h",
                    $"{extended.Game}: faltan votos. Tienes 24 horas más para votar.",
                    extended.GroupId,
                    extended.Id,
                    cancellationToken: cancellationToken);
                return extended;

            case VotingDecision.Tie:
                return await HandleTieAsync(contract, now, cancellationToken);

            default:
                return await OpenAdminPhaseAsync(contract, now, cancellationToken);
        }
    }

    /// <summary>
    /// Empate (CLAR #81): decide un administrador que no sea parte. Si no hay ninguno, se asigna un jurado
    /// adicional una sola vez; si tampoco es posible, se abre la fase del administrador (y vence a cancelled).
    /// </summary>
    public async Task<Contract> HandleTieAsync(Contract contract, DateTime now, CancellationToken cancellationToken)
    {
        var (members, _) = await _members.ListByGroupAsync(contract.GroupId, null, 50, null, cancellationToken);
        var parties = Parties(contract);
        var neutralAdmins = members.Where(x => x.Role == "admin" && !parties.Contains(x.UserId)).Select(x => x.UserId).ToList();
        if (neutralAdmins.Count == 0 && !contract.ExtraJurorAssigned)
        {
            var eligible = members
                .Select(x => x.UserId)
                .Where(x => !parties.Contains(x) && !contract.Jurors.Contains(x))
                .ToArray();
            if (eligible.Length > 0)
            {
                RandomNumberGenerator.Shuffle(eligible.AsSpan());
                var updated = await _contracts.TryAddExtraJurorAsync(contract.Id, eligible[0], now, cancellationToken);
                if (updated is not null)
                {
                    await _audit.AppendAsync("contract.extra_juror", null, "contract", updated.Id, updated.GroupId, new { contract_id = updated.Id.ToString() }, cancellationToken);
                    await _notifications.NotifyAsync(
                        [eligible[0]],
                        "vote_requested",
                        "Te toca desempatar",
                        $"{updated.Game}: el jurado empató. Tu voto decide.",
                        updated.GroupId,
                        updated.Id,
                        cancellationToken: cancellationToken,
                        dedupeSuffix: "extra");
                    return updated;
                }
            }
        }

        return await OpenAdminPhaseAsync(contract, now, cancellationToken);
    }

    public async Task<Contract> OpenAdminPhaseAsync(Contract contract, DateTime now, CancellationToken cancellationToken)
    {
        var updated = await _contracts.TryStartAdminPhaseAsync(contract.Id, now.AddHours(_options.AdminDecisionHours), now, cancellationToken);
        if (updated is null)
        {
            return contract;
        }

        var (members, _) = await _members.ListByGroupAsync(updated.GroupId, null, 50, null, cancellationToken);
        var parties = Parties(updated);
        await _notifications.NotifyAsync(
            members.Where(x => x.Role == "admin" && !parties.Contains(x.UserId)).Select(x => x.UserId),
            "vote_tie",
            "Decide como administrador",
            $"{updated.Game}: el jurado no llegó a consenso. Tienes 48 horas para decidir.",
            updated.GroupId,
            updated.Id,
            cancellationToken: cancellationToken);
        await _cache.BumpVersionAsync(CacheExtensions.GroupScope(updated.GroupId), cancellationToken);
        return updated;
    }

    public async Task<Contract?> ResolveAsync(Contract contract, string outcome, string resolvedBy, ObjectId? actor, CancellationToken cancellationToken)
    {
        var resolved = await _contracts.ResolveAsync(contract.Id, outcome, resolvedBy, DateTime.UtcNow, cancellationToken);
        if (resolved is null)
        {
            return null;
        }

        var fulfilled = outcome == "fulfilled";
        await _reputation.OnContractResolvedAsync(resolved, resolvedBy, cancellationToken);
        await _audit.AppendAsync(
            "contract.resolved",
            actor,
            "contract",
            resolved.Id,
            resolved.GroupId,
            new { contract_id = resolved.Id.ToString(), outcome, resolved_by = resolvedBy },
            cancellationToken);
        await NotifyPartiesAsync(
            resolved,
            "contract_resolved",
            fulfilled ? "¡Reto cumplido!" : "Reto marcado como trampa",
            fulfilled
                ? $"{resolved.Game}: el jurado lo validó. Ganas la fianza de vuelta más un premio igual. Recompensa: {resolved.Reward}"
                : $"{resolved.Game}: se detectó trampa; la fianza pasa al retador.",
            cancellationToken);
        return resolved;
    }

    private async Task ExpireAsync(Contract contract, DateTime now, CancellationToken cancellationToken)
    {
        var expired = await _contracts.TryExpireAsync(contract.Id, now, cancellationToken);
        if (expired is null)
        {
            return;
        }

        await _reputation.OnContractExpiredAsync(expired, cancellationToken);

        await AfterCloseAsync(
            expired,
            "contract.expired",
            "contract_expired",
            "Reto vencido",
            expired.AcceptedBy is null || expired.BondRequired == 0
                ? $"{expired.Game}: venció sin evidencia."
                : $"{expired.Game}: venció sin evidencia; la fianza pasa al retador.",
            cancellationToken);
    }

    private async Task AfterCloseAsync(Contract contract, string auditAction, string type, string title, string body, CancellationToken cancellationToken)
    {
        await _audit.AppendAsync(auditAction, null, "contract", contract.Id, contract.GroupId, new { contract_id = contract.Id.ToString(), reason = contract.ClosedReason }, cancellationToken);
        await NotifyPartiesAsync(contract, type, title, body, cancellationToken);
    }

    private async Task NotifyPartiesAsync(Contract contract, string type, string title, string body, CancellationToken cancellationToken)
    {
        var parties = Parties(contract);
        foreach (var party in parties)
        {
            await _cache.BumpVersionAsync(CacheExtensions.UserScope(party), cancellationToken);
        }

        await _cache.BumpVersionAsync(CacheExtensions.GroupScope(contract.GroupId), cancellationToken);
        await _notifications.NotifyAsync(parties, type, title, body, contract.GroupId, contract.Id, cancellationToken: cancellationToken);
    }

    /// <summary>Retador, quien aceptó/cumple y destinatario directo.</summary>
    public static HashSet<ObjectId> Parties(Contract contract)
    {
        var parties = new HashSet<ObjectId> { contract.ChallengerId };
        foreach (var id in new[] { contract.ExecutorId, contract.AcceptedBy, contract.TargetId })
        {
            if (id is ObjectId value)
            {
                parties.Add(value);
            }
        }

        return parties;
    }

    private static IEnumerable<ObjectId> PendingJurors(Contract contract) =>
        contract.Jurors.Where(x => !contract.Voters.Contains(x));
}
