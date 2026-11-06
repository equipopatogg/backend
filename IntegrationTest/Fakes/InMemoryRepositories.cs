using System.Collections.Concurrent;
using MongoDB.Bson;
using Pactogg.Api.Infrastructure.Audit;
using Pactogg.Api.Infrastructure.Moderation;
using Pactogg.Api.Infrastructure.Storage;
using Pactogg.Api.Modules.Auth.Abstractions;
using Pactogg.Api.Modules.Auth.Domain;
using Pactogg.Api.Modules.Contracts.Abstractions;
using Pactogg.Api.Modules.Contracts.Domain;
using Pactogg.Api.Modules.Evidence.Abstractions;
using Pactogg.Api.Modules.Groups.Abstractions;
using Pactogg.Api.Modules.Groups.Domain;
using Pactogg.Api.Modules.Points.Abstractions;
using Pactogg.Api.Modules.Points.Domain;
using Pactogg.Api.Modules.Votes.Abstractions;
using Pactogg.Api.Modules.Votes.Domain;
using Pactogg.Api.Modules.Commitments.Abstractions;
using Pactogg.Api.Modules.Commitments.Domain;
using Pactogg.Api.Modules.Notifications.Abstractions;
using Pactogg.Api.Modules.Notifications.Domain;
using Pactogg.Api.Modules.Tournaments.Abstractions;
using Pactogg.Api.Modules.Tournaments.Domain;
using EvidenceEntity = Pactogg.Api.Modules.Evidence.Domain.Evidence;

namespace IntegrationTest.Fakes;

public sealed class InMemoryUserRepository : IUserRepository
{
    private readonly ConcurrentDictionary<ObjectId, User> _users = new();

    public Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        Task.FromResult(_users.Values.FirstOrDefault(x => x.Email == normalizedEmail && x.DeletedAt is null));

    public Task<User?> GetByIdAsync(ObjectId id, CancellationToken cancellationToken) =>
        Task.FromResult(_users.GetValueOrDefault(id));

    public Task<User?> GetByNicknameAsync(string nicknameLower, CancellationToken cancellationToken) =>
        Task.FromResult(_users.Values.FirstOrDefault(x => x.NicknameLower == nicknameLower && x.DeletedAt is null));

    public Task<User?> GetByGoogleSubAsync(string googleSub, CancellationToken cancellationToken) =>
        Task.FromResult(_users.Values.FirstOrDefault(x => x.GoogleSub == googleSub && x.DeletedAt is null));

    public Task LinkGoogleAsync(ObjectId id, string googleSub, DateTime at, CancellationToken cancellationToken)
    {
        if (_users.TryGetValue(id, out var user))
        {
            user.GoogleSub = googleSub;
            user.EmailVerified = true;
            user.UpdatedAt = at;
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<User>> ListByIdsAsync(IReadOnlyCollection<ObjectId> ids, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<User>>(_users.Values.Where(x => ids.Contains(x.Id)).ToList());

    public Task SetEmailVerifiedAsync(ObjectId id, DateTime at, CancellationToken cancellationToken)
    {
        if (_users.TryGetValue(id, out var user))
        {
            user.EmailVerified = true;
            user.UpdatedAt = at;
        }

        return Task.CompletedTask;
    }

    public Task UpdatePasswordAsync(ObjectId id, string passwordHash, DateTime at, CancellationToken cancellationToken)
    {
        if (_users.TryGetValue(id, out var user))
        {
            user.PasswordHash = passwordHash;
            user.PasswordChangedAt = at;
            user.UpdatedAt = at;
        }

        return Task.CompletedTask;
    }

    public Task UpdateConsentAsync(ObjectId id, string version, DateTime at, CancellationToken cancellationToken)
    {
        if (_users.TryGetValue(id, out var user))
        {
            user.Consent = new ConsentRecord { Version = version, AcceptedAt = at };
        }

        return Task.CompletedTask;
    }

    public Task<User?> UpdateNotificationPreferencesAsync(ObjectId id, NotificationPreferences preferences, DateTime at, CancellationToken cancellationToken)
    {
        if (!_users.TryGetValue(id, out var user))
        {
            return Task.FromResult<User?>(null);
        }

        user.NotificationPreferences = preferences;
        return Task.FromResult<User?>(user);
    }

    public Task<User?> UpdateProfileAsync(ObjectId id, string? nickname, string? avatarId, DateTime at, CancellationToken cancellationToken)
    {
        lock (_users)
        {
            if (!_users.TryGetValue(id, out var user))
            {
                return Task.FromResult<User?>(null);
            }

            if (nickname is not null)
            {
                var lower = nickname.ToLowerInvariant();
                if (_users.Values.Any(x => x.Id != id && x.NicknameLower == lower))
                {
                    throw Pactogg.Api.Common.ApiException.Conflict("NICKNAME_ALREADY_EXISTS", "El apodo ya está en uso.");
                }

                user.Nickname = nickname;
                user.NicknameLower = lower;
            }

            if (avatarId is not null)
            {
                user.AvatarId = avatarId;
            }

            user.UpdatedAt = at;
            return Task.FromResult<User?>(user);
        }
    }

    public Task CreateAsync(User user, CancellationToken cancellationToken)
    {
        if (_users.Values.Any(x => x.Email == user.Email))
        {
            throw Pactogg.Api.Common.ApiException.Conflict("EMAIL_ALREADY_EXISTS", "Ya existe una cuenta con ese correo.");
        }

        if (_users.Values.Any(x => x.NicknameLower == user.NicknameLower))
        {
            throw Pactogg.Api.Common.ApiException.Conflict("NICKNAME_ALREADY_EXISTS", "El apodo ya está en uso.");
        }

        _users[user.Id] = user;
        return Task.CompletedTask;
    }

    public Task UpdateLastLoginAsync(ObjectId id, DateTime at, CancellationToken cancellationToken)
    {
        if (_users.TryGetValue(id, out var user))
        {
            user.LastLoginAt = at;
            user.UpdatedAt = at;
        }

        return Task.CompletedTask;
    }
}

public sealed class InMemoryRefreshTokenRepository : IRefreshTokenRepository
{
    private readonly ConcurrentDictionary<ObjectId, RefreshToken> _tokens = new();

    public Task CreateAsync(RefreshToken token, CancellationToken cancellationToken)
    {
        _tokens[token.Id] = token;
        return Task.CompletedTask;
    }

    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        Task.FromResult(_tokens.Values.FirstOrDefault(x => x.TokenHash == tokenHash));

    public Task RevokeAsync(ObjectId id, DateTime at, CancellationToken cancellationToken)
    {
        if (_tokens.TryGetValue(id, out var token))
        {
            token.RevokedAt = at;
        }

        return Task.CompletedTask;
    }

    public Task MarkRotatedAsync(ObjectId id, DateTime at, CancellationToken cancellationToken)
    {
        var token = _tokens.Values.FirstOrDefault(x => x.Id == id);
        if (token is not null && token.RevokedAt is null)
        {
            token.RevokedAt = at;
            token.Rotated = true;
        }

        return Task.CompletedTask;
    }

    public Task RevokeAllForUserAsync(ObjectId userId, DateTime at, CancellationToken cancellationToken)
    {
        foreach (var token in _tokens.Values.Where(x => x.UserId == userId))
        {
            token.RevokedAt = at;
        }

        return Task.CompletedTask;
    }
}

public sealed class InMemoryGroupRepository : IGroupRepository
{
    private readonly ConcurrentDictionary<ObjectId, Group> _groups = new();

    public Task<Group?> GetByIdAsync(ObjectId id, CancellationToken cancellationToken) =>
        Task.FromResult(_groups.GetValueOrDefault(id));

    public Task CreateAsync(Group group, CancellationToken cancellationToken)
    {
        _groups[group.Id] = group;
        return Task.CompletedTask;
    }

    public Task<bool> InviteCodeExistsAsync(string inviteCode, CancellationToken cancellationToken) =>
        Task.FromResult(_groups.Values.Any(x => x.InviteCode == inviteCode));

    public Task<Group?> GetByInviteCodeAsync(string inviteCode, CancellationToken cancellationToken) =>
        Task.FromResult(_groups.Values.FirstOrDefault(x => x.InviteCode == inviteCode && x.InviteActive && x.DeletedAt is null));

    public Task<(IReadOnlyList<Group> Items, bool HasMore)> ListByIdsAsync(
        IReadOnlyCollection<ObjectId> ids,
        string? nameContains,
        int limit,
        ObjectId? cursor,
        CancellationToken cancellationToken)
    {
        var query = _groups.Values.Where(x => ids.Contains(x.Id) && x.DeletedAt is null);
        if (!string.IsNullOrWhiteSpace(nameContains))
        {
            query = query.Where(x => x.Name.Contains(nameContains, StringComparison.OrdinalIgnoreCase));
        }

        if (cursor.HasValue)
        {
            query = query.Where(x => x.Id < cursor.Value);
        }

        var items = query.OrderByDescending(x => x.Id).Take(limit + 1).ToList();
        return Task.FromResult<(IReadOnlyList<Group>, bool)>((items.Take(limit).ToList(), items.Count > limit));
    }

    public Task<Group?> TryIncrementMemberCountAsync(ObjectId id, int maxMembers, DateTime at, CancellationToken cancellationToken)
    {
        lock (_groups)
        {
            if (!_groups.TryGetValue(id, out var group) || group.MemberCount >= maxMembers)
            {
                return Task.FromResult<Group?>(null);
            }

            group.MemberCount++;
            group.UpdatedAt = at;
            return Task.FromResult<Group?>(group);
        }
    }

    public Task DecrementMemberCountAsync(ObjectId id, CancellationToken cancellationToken)
    {
        lock (_groups)
        {
            if (_groups.TryGetValue(id, out var group) && group.MemberCount > 0)
            {
                group.MemberCount--;
            }
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(ObjectId id, CancellationToken cancellationToken)
    {
        _groups.TryRemove(id, out _);
        return Task.CompletedTask;
    }
}

public sealed class InMemoryGroupMemberRepository : IGroupMemberRepository
{
    private readonly ConcurrentDictionary<ObjectId, GroupMember> _members = new();

    public Task<GroupMember?> GetAsync(ObjectId groupId, ObjectId userId, CancellationToken cancellationToken) =>
        Task.FromResult(_members.Values.FirstOrDefault(x => x.GroupId == groupId && x.UserId == userId && x.Status == "active"));

    public Task<int> CountActiveGroupsAsync(ObjectId userId, CancellationToken cancellationToken) =>
        Task.FromResult(_members.Values.Count(x => x.UserId == userId && x.Status == "active"));

    public Task<IReadOnlyList<ObjectId>> ListActiveGroupIdsAsync(ObjectId userId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ObjectId>>(_members.Values
            .Where(x => x.UserId == userId && x.Status == "active")
            .Select(x => x.GroupId)
            .Distinct()
            .ToList());

    public Task<IReadOnlyList<GroupMember>> ListForUserAsync(ObjectId userId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<GroupMember>>(_members.Values.Where(x => x.UserId == userId && x.Status == "active").ToList());

    public Task<(IReadOnlyList<GroupMember> Items, bool HasMore)> ListByGroupAsync(
        ObjectId groupId,
        string? nickPrefix,
        int limit,
        ObjectId? cursor,
        CancellationToken cancellationToken)
    {
        var query = _members.Values.Where(x => x.GroupId == groupId && x.Status == "active");
        if (!string.IsNullOrWhiteSpace(nickPrefix))
        {
            query = query.Where(x => x.NickLower.StartsWith(nickPrefix, StringComparison.Ordinal));
        }

        if (cursor.HasValue)
        {
            query = query.Where(x => x.Id > cursor.Value);
        }

        var items = query.OrderBy(x => x.Id).Take(limit + 1).ToList();
        return Task.FromResult<(IReadOnlyList<GroupMember>, bool)>((items.Take(limit).ToList(), items.Count > limit));
    }

    public Task CreateAsync(GroupMember member, CancellationToken cancellationToken)
    {
        lock (_members)
        {
            if (_members.Values.Any(x => x.GroupId == member.GroupId && x.UserId == member.UserId))
            {
                throw Pactogg.Api.Common.ApiException.Conflict("ALREADY_MEMBER", "Ya perteneces a este grupo.");
            }

            _members[member.Id] = member;
        }

        return Task.CompletedTask;
    }

    public Task UpdateNickLowerAsync(ObjectId userId, string nickLower, CancellationToken cancellationToken)
    {
        foreach (var member in _members.Values.Where(x => x.UserId == userId))
        {
            member.NickLower = nickLower;
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<GroupMember>> ListRankingAsync(ObjectId groupId, int limit, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<GroupMember>>(_members.Values
            .Where(x => x.GroupId == groupId && x.Status == "active")
            .OrderByDescending(x => x.PointsBalance)
            .ThenBy(x => x.JoinedAt)
            .Take(limit)
            .ToList());

    public GroupMember? Find(ObjectId groupId, ObjectId userId) =>
        _members.Values.FirstOrDefault(x => x.GroupId == groupId && x.UserId == userId);

    /// <summary>Solo para pruebas: fija el saldo de un miembro.</summary>
    public void SetBalance(ObjectId groupId, ObjectId userId, int balance)
    {
        var member = _members.Values.First(x => x.GroupId == groupId && x.UserId == userId);
        member.PointsBalance = balance;
    }
}

public sealed class InMemoryContractRepository : IContractRepository
{
    private readonly ConcurrentDictionary<ObjectId, Contract> _contracts = new();
    private readonly InMemoryGroupMemberRepository _members;
    private readonly InMemoryUserRepository _users;
    private readonly InMemoryCommitmentRepository _commitments;
    private readonly InMemoryPointsLedgerRepository _ledger;

    public InMemoryContractRepository(
        InMemoryGroupMemberRepository members,
        InMemoryUserRepository users,
        InMemoryCommitmentRepository commitments,
        InMemoryPointsLedgerRepository ledger)
    {
        _members = members;
        _users = users;
        _commitments = commitments;
        _ledger = ledger;
    }

    public ConcurrentBag<PointsLedgerEntry> Ledger => _ledger.Entries;

    public Task<Contract?> TryRegisterVoteAsync(ObjectId id, ObjectId jurorId, bool legit, DateTime now, CancellationToken cancellationToken)
    {
        lock (_contracts)
        {
            if (!_contracts.TryGetValue(id, out var contract)
                || contract.Status != "voting"
                || !contract.Jurors.Contains(jurorId)
                || contract.Voters.Contains(jurorId))
            {
                return Task.FromResult<Contract?>(null);
            }

            contract.Voters.Add(jurorId);
            if (legit)
            {
                contract.LegitVotes++;
            }
            else
            {
                contract.CheatVotes++;
            }

            contract.Version++;
            contract.UpdatedAt = now;
            return Task.FromResult<Contract?>(contract);
        }
    }

    public Task<(IReadOnlyList<Contract> Items, bool HasMore)> ListPendingVotesAsync(ObjectId jurorId, int limit, ObjectId? cursor, CancellationToken cancellationToken)
    {
        var items = _contracts.Values
            .Where(x => x.Status == "voting" && x.Jurors.Contains(jurorId) && !x.Voters.Contains(jurorId))
            .Where(x => !cursor.HasValue || x.Id < cursor.Value)
            .OrderByDescending(x => x.Id)
            .Take(limit + 1)
            .ToList();
        return Task.FromResult<(IReadOnlyList<Contract>, bool)>((items.Take(limit).ToList(), items.Count > limit));
    }

    public Task<(IReadOnlyList<Contract> Items, bool HasMore)> ListFulfilledAsync(ObjectId groupId, int limit, ObjectId? cursor, CancellationToken cancellationToken)
    {
        var items = _contracts.Values
            .Where(x => x.GroupId == groupId && x.Status == "fulfilled")
            .Where(x => !cursor.HasValue || x.Id < cursor.Value)
            .OrderByDescending(x => x.Id)
            .Take(limit + 1)
            .ToList();
        return Task.FromResult<(IReadOnlyList<Contract>, bool)>((items.Take(limit).ToList(), items.Count > limit));
    }

    public async Task<Contract?> ResolveAsync(ObjectId id, string outcome, string resolvedBy, DateTime now, CancellationToken cancellationToken)
    {
        Contract contract;
        lock (_contracts)
        {
            if (!_contracts.TryGetValue(id, out var found) || found.Status != "voting")
            {
                return null;
            }

            contract = found;
            contract.Status = outcome;
            contract.ResolvedBy = resolvedBy;
            contract.ResolvedAt = now;
            contract.UpdatedAt = now;
            contract.Version++;
        }

        var fulfilled = outcome == "fulfilled";
        if (contract.AcceptedBy is ObjectId acceptor && contract.BondRequired > 0)
        {
            if (fulfilled)
            {
                Credit(contract, acceptor, contract.BondRequired, "bond_release", now);
                Credit(contract, acceptor, contract.BondRequired, "reward", now);
            }
            else
            {
                Credit(contract, contract.ChallengerId, contract.BondRequired, "bond_forfeit", now);
            }
        }

        var executor = contract.ExecutorId ?? contract.AcceptedBy ?? contract.TargetId;
        if (executor is ObjectId executorId)
        {
            if (fulfilled && executorId != contract.ChallengerId)
            {
                _commitments.Add(new Commitment
                {
                    Id = ObjectId.GenerateNewId(),
                    GroupId = contract.GroupId,
                    ContractId = contract.Id,
                    DebtorId = contract.ChallengerId,
                    CreditorId = executorId,
                    Description = contract.Reward,
                    Game = contract.Game,
                    Status = "pending",
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }

            var user = await _users.GetByIdAsync(executorId, cancellationToken);
            if (user is not null)
            {
                if (fulfilled)
                {
                    user.Stats.Completed++;
                    user.Stats.Streak++;
                }
                else
                {
                    user.Stats.Cheated++;
                    user.Stats.Streak = 0;
                }
            }
        }

        return contract;
    }

    private void Credit(Contract contract, ObjectId userId, int amount, string type, DateTime now)
    {
        var member = _members.Find(contract.GroupId, userId);
        if (member is null)
        {
            return;
        }

        member.PointsBalance += amount;
        _ledger.Entries.Add(new PointsLedgerEntry
        {
            Id = ObjectId.GenerateNewId(),
            GroupId = contract.GroupId,
            UserId = userId,
            Type = type,
            Amount = amount,
            RefType = "contract",
            RefId = contract.Id,
            BalanceAfter = member.PointsBalance,
            CreatedAt = now
        });
    }

    public Task<IReadOnlyList<Contract>> ListPastDeadlineAsync(DateTime now, int limit, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Contract>>(_contracts.Values
            .Where(x => (x.Status == "open" || x.Status == "in_progress") && x.Deadline <= now)
            .Take(limit).ToList());

    public Task<IReadOnlyList<Contract>> ListVotingDueAsync(DateTime now, DateTime reminderThreshold, int limit, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Contract>>(_contracts.Values
            .Where(x => x.Status == "voting"
                && (x.VotingEndsAt <= now || x.AdminDeadlineAt <= now || (x.VotingEndsAt <= reminderThreshold && !x.VoteReminderSent)))
            .Take(limit).ToList());

    public Task<Contract?> TryExpireAsync(ObjectId id, DateTime now, CancellationToken cancellationToken)
    {
        Contract contract;
        lock (_contracts)
        {
            if (!_contracts.TryGetValue(id, out var found) || (found.Status != "open" && found.Status != "in_progress") || found.Deadline > now)
            {
                return Task.FromResult<Contract?>(null);
            }

            contract = found;
            contract.Status = "expired";
            contract.ClosedReason = "deadline_without_evidence";
            contract.ResolvedBy = "system";
            contract.ResolvedAt = now;
            contract.UpdatedAt = now;
            contract.Version++;
        }

        if (contract is { AcceptedBy: ObjectId acceptor, BondRequired: > 0 })
        {
            Credit(contract, contract.ChallengerId, contract.BondRequired, "bond_forfeit", now);
            var user = _users.GetByIdAsync(acceptor, cancellationToken).Result;
            if (user is not null)
            {
                user.Stats.Streak = 0;
            }
        }

        return Task.FromResult<Contract?>(contract);
    }

    public Task<Contract?> TryExtendVotingAsync(ObjectId id, DateTime newEnd, DateTime now, CancellationToken cancellationToken) =>
        Mutate(id, x => x.Status == "voting" && !x.VotingExtended, x =>
        {
            x.VotingExtended = true;
            x.VotingEndsAt = newEnd;
            x.VoteReminderSent = false;
            x.UpdatedAt = now;
        });

    public Task<Contract?> TryStartAdminPhaseAsync(ObjectId id, DateTime adminDeadline, DateTime now, CancellationToken cancellationToken) =>
        Mutate(id, x => x.Status == "voting" && x.AdminDeadlineAt == null, x =>
        {
            x.AdminDeadlineAt = adminDeadline;
            x.UpdatedAt = now;
        });

    public Task<Contract?> TryAddExtraJurorAsync(ObjectId id, ObjectId jurorId, DateTime now, CancellationToken cancellationToken) =>
        Mutate(id, x => x.Status == "voting" && !x.ExtraJurorAssigned, x =>
        {
            if (!x.Jurors.Contains(jurorId))
            {
                x.Jurors.Add(jurorId);
            }

            x.ExtraJurorAssigned = true;
            x.VotingEndsAt = now.AddHours(24);
            x.UpdatedAt = now;
        });

    public Task MarkReminderSentAsync(ObjectId id, CancellationToken cancellationToken)
    {
        if (_contracts.TryGetValue(id, out var contract))
        {
            contract.VoteReminderSent = true;
        }

        return Task.CompletedTask;
    }

    public async Task<Contract?> TryCancelVotingWithRefundAsync(ObjectId id, string reason, DateTime now, CancellationToken cancellationToken)
    {
        var contract = await Mutate(id, x => x.Status == "voting", x =>
        {
            x.Status = "cancelled";
            x.ClosedReason = reason;
            x.ResolvedBy = "system";
            x.ResolvedAt = now;
            x.UpdatedAt = now;
        });

        if (contract is { AcceptedBy: ObjectId acceptor, BondRequired: > 0 })
        {
            Credit(contract, acceptor, contract.BondRequired, "bond_release", now);
        }

        return contract;
    }

    public Task<Contract?> TryUpdateOpenAsync(ObjectId id, int expectedVersion, string game, string condition, string reward, DateTime deadline, int bond, DateTime now, CancellationToken cancellationToken) =>
        Mutate(id, x => x.Status == "open" && x.Version == expectedVersion && x.AcceptedBy == null, x =>
        {
            x.Game = game;
            x.Condition = condition;
            x.Reward = reward;
            x.Deadline = deadline;
            x.BondRequired = bond;
            x.UpdatedAt = now;
        });

    public Task<Contract?> TryCancelOpenAsync(ObjectId id, int expectedVersion, DateTime now, CancellationToken cancellationToken) =>
        Mutate(id, x => x.Status == "open" && x.Version == expectedVersion && x.AcceptedBy == null, x =>
        {
            x.Status = "cancelled";
            x.ClosedReason = "cancelled_by_challenger";
            x.ResolvedAt = now;
            x.UpdatedAt = now;
        });

    private Task<Contract?> Mutate(ObjectId id, Func<Contract, bool> when, Action<Contract> apply)
    {
        lock (_contracts)
        {
            if (!_contracts.TryGetValue(id, out var found) || !when(found))
            {
                return Task.FromResult<Contract?>(null);
            }

            apply(found);
            found.Version++;
            return Task.FromResult<Contract?>(found);
        }
    }

    public Task<(IReadOnlyList<Contract> Items, bool HasMore)> ListForGroupAsync(
        ObjectId groupId,
        ContractListFilter filter,
        int limit,
        ObjectId? cursor,
        CancellationToken cancellationToken)
    {
        var query = _contracts.Values.Where(x => x.GroupId == groupId);
        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            query = query.Where(x => x.Status == filter.Status);
        }

        if (filter.Mine.HasValue)
        {
            var me = filter.Mine.Value;
            query = query.Where(x => x.ChallengerId == me || x.TargetId == me || x.AcceptedBy == me);
        }

        if (!string.IsNullOrWhiteSpace(filter.Game))
        {
            query = query.Where(x => string.Equals(x.Game, filter.Game, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(filter.Query))
        {
            var q = filter.Query;
            query = query.Where(x => x.Condition.Contains(q, StringComparison.OrdinalIgnoreCase)
                || x.Reward.Contains(q, StringComparison.OrdinalIgnoreCase)
                || x.Game.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        if (cursor.HasValue)
        {
            query = query.Where(x => x.Id < cursor.Value);
        }

        var items = query.OrderByDescending(x => x.Id).Take(limit + 1).ToList();
        return Task.FromResult<(IReadOnlyList<Contract>, bool)>((items.Take(limit).ToList(), items.Count > limit));
    }

    public async Task<AcceptContractResult> AcceptWithBondAsync(
        ObjectId contractId,
        int expectedVersion,
        ObjectId userId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (!_contracts.TryGetValue(contractId, out var contract))
        {
            return new AcceptContractResult(AcceptContractOutcome.StateChanged, null, 0);
        }

        var member = await _members.GetAsync(contract.GroupId, userId, cancellationToken);
        lock (_contracts)
        {
            if (contract.Status != "open" || contract.Version != expectedVersion)
            {
                return new AcceptContractResult(AcceptContractOutcome.StateChanged, null, 0);
            }

            if (member is null || member.PointsBalance < contract.BondRequired)
            {
                return new AcceptContractResult(AcceptContractOutcome.InsufficientPoints, null, 0);
            }

            member.PointsBalance -= contract.BondRequired;
            contract.Status = "in_progress";
            contract.AcceptedBy = userId;
            contract.AcceptedAt = now;
            contract.UpdatedAt = now;
            contract.Version++;
            Ledger.Add(new PointsLedgerEntry
            {
                Id = ObjectId.GenerateNewId(),
                GroupId = contract.GroupId,
                UserId = userId,
                Type = "bond_lock",
                Amount = -contract.BondRequired,
                RefType = "contract",
                RefId = contract.Id,
                BalanceAfter = member.PointsBalance,
                CreatedAt = now
            });
            return new AcceptContractResult(AcceptContractOutcome.Accepted, contract, member.PointsBalance);
        }
    }

    public Task<Contract?> TryRejectAsync(ObjectId contractId, int expectedVersion, ObjectId userId, DateTime now, CancellationToken cancellationToken)
    {
        lock (_contracts)
        {
            if (!_contracts.TryGetValue(contractId, out var contract) || contract.Status != "open" || contract.Version != expectedVersion)
            {
                return Task.FromResult<Contract?>(null);
            }

            contract.Status = "cancelled";
            contract.RejectedBy = userId;
            contract.ClosedReason = "rejected";
            contract.UpdatedAt = now;
            contract.Version++;
            return Task.FromResult<Contract?>(contract);
        }
    }

    public Task CreateAsync(Contract contract, CancellationToken cancellationToken)
    {
        _contracts[contract.Id] = contract;
        return Task.CompletedTask;
    }

    public Task<Contract?> GetByIdAsync(ObjectId id, CancellationToken cancellationToken) =>
        Task.FromResult(_contracts.GetValueOrDefault(id));

    public Task<Contract?> TryAttachEvidenceAsync(
        ObjectId id,
        ObjectId userId,
        DateTime now,
        int maxEvidence,
        CancellationToken cancellationToken)
    {
        if (!_contracts.TryGetValue(id, out var contract)
            || contract.EvidenceCount >= maxEvidence
            || contract.Status is not ("open" or "accepted" or "in_progress" or "evidence_pending" or "voting"))
        {
            return Task.FromResult<Contract?>(null);
        }

        contract.EvidenceCount++;
        contract.UpdatedAt = now;
        return Task.FromResult<Contract?>(contract);
    }

    public Task DecrementEvidenceAsync(ObjectId id, CancellationToken cancellationToken)
    {
        if (_contracts.TryGetValue(id, out var contract) && contract.EvidenceCount > 0)
        {
            contract.EvidenceCount--;
        }

        return Task.CompletedTask;
    }

    public Task MarkVotingAsync(ObjectId id, ObjectId executorId, IReadOnlyList<ObjectId> jurors, DateTime votingEndsAt, CancellationToken cancellationToken)
    {
        lock (_contracts)
        {
            if (_contracts.TryGetValue(id, out var contract) && contract.Status != "voting")
            {
                contract.Status = "voting";
                contract.ExecutorId = executorId;
                contract.Jurors = jurors.ToList();
                contract.Voters = [];
                contract.VotingStartedAt = DateTime.UtcNow;
                contract.VotingEndsAt = votingEndsAt;
                contract.UpdatedAt = DateTime.UtcNow;
                contract.Version++;
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>Solo para pruebas.</summary>
    public Contract Get(ObjectId id) => _contracts[id];
}

public sealed class InMemoryEvidenceRepository : IEvidenceRepository
{
    private readonly ConcurrentDictionary<ObjectId, EvidenceEntity> _evidence = new();

    public Task CreateAsync(EvidenceEntity evidence, CancellationToken cancellationToken)
    {
        _evidence[evidence.Id] = evidence;
        return Task.CompletedTask;
    }

    public Task<int> CountByContractAsync(ObjectId contractId, CancellationToken cancellationToken) =>
        Task.FromResult(_evidence.Values.Count(x => x.ContractId == contractId));

    public Task<IReadOnlyList<EvidenceEntity>> ListByContractIdsAsync(
        IReadOnlyCollection<ObjectId> contractIds,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<EvidenceEntity>>(_evidence.Values
            .Where(x => contractIds.Contains(x.ContractId))
            .OrderBy(x => x.CreatedAt)
            .ToList());
}

public sealed class InMemoryTournamentRepository : ITournamentRepository
{
    private readonly ConcurrentDictionary<ObjectId, Tournament> _tournaments = new();

    public void Add(Tournament tournament) => _tournaments[tournament.Id] = tournament;

    public Task<(IReadOnlyList<Tournament> Items, bool HasMore)> ListForGroupsAsync(
        IReadOnlyCollection<ObjectId> groupIds,
        string? status,
        string? game,
        ObjectId? groupId,
        int limit,
        ObjectId? cursor,
        CancellationToken cancellationToken)
    {
        var query = _tournaments.Values.Where(x => groupIds.Contains(x.GroupId));
        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(x => x.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(game))
        {
            query = query.Where(x => x.Game == game);
        }

        if (groupId.HasValue)
        {
            query = query.Where(x => x.GroupId == groupId.Value);
        }

        if (cursor.HasValue)
        {
            query = query.Where(x => x.Id < cursor.Value);
        }

        var items = query.OrderByDescending(x => x.Id).Take(limit + 1).ToList();
        return Task.FromResult<(IReadOnlyList<Tournament>, bool)>((items.Take(limit).ToList(), items.Count > limit));
    }
}

public sealed class InMemoryMatchRepository : IMatchRepository
{
    private readonly List<Match> _matches = [];

    public void Add(Match match) => _matches.Add(match);

    public Task<IReadOnlyList<Match>> ListForTournamentIdsAsync(
        IReadOnlyCollection<ObjectId> tournamentIds,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Match>>(_matches
            .Where(x => tournamentIds.Contains(x.TournamentId))
            .OrderBy(x => x.Round)
            .ThenBy(x => x.Position)
            .ToList());
}

public sealed class FakeAuditService : IAuditService, IAuditReader
{
    private readonly List<AuditEvent> _events = [];

    public Task AppendAsync(
        string action,
        ObjectId? actorId,
        string entityType,
        ObjectId? entityId,
        ObjectId? groupId,
        object? payload,
        CancellationToken cancellationToken)
    {
        lock (_events)
        {
            _events.Add(new AuditEvent
            {
                Id = ObjectId.GenerateNewId(),
                Action = action,
                ActorId = actorId,
                EntityType = entityType,
                EntityId = entityId,
                GroupId = groupId,
                Payload = payload is null ? "{}" : System.Text.Json.JsonSerializer.Serialize(payload),
                CreatedAt = DateTime.UtcNow
            });
        }

        return Task.CompletedTask;
    }

    public Task<(IReadOnlyList<AuditEvent> Items, bool HasMore)> ListByGroupAsync(
        ObjectId groupId,
        string? entityType,
        ObjectId? entityId,
        int limit,
        ObjectId? cursor,
        CancellationToken cancellationToken)
    {
        lock (_events)
        {
            var query = _events.Where(x => x.GroupId == groupId
                && (entityType == null || x.EntityType == entityType)
                && (entityId == null || x.EntityId == entityId)
                && (cursor == null || x.Id < cursor))
                .OrderByDescending(x => x.Id)
                .Take(limit + 1)
                .ToList();
            var hasMore = query.Count > limit;
            return Task.FromResult<(IReadOnlyList<AuditEvent>, bool)>((hasMore ? query.Take(limit).ToList() : query, hasMore));
        }
    }
}

public sealed class FakeModerationService : IModerationService
{
    public bool RejectImages { get; set; }

    public Task<ModerationResult> ModerateImageAsync(byte[] image, CancellationToken cancellationToken) =>
        Task.FromResult(RejectImages ? ModerationResult.Reject("test") : ModerationResult.Allow());

    public Task<ModerationResult> ModerateTextAsync(string text, CancellationToken cancellationToken) =>
        Task.FromResult(text.Contains("reject", StringComparison.OrdinalIgnoreCase)
            ? ModerationResult.Reject("test")
            : ModerationResult.Allow());
}

public sealed class FakeObjectStorage : IObjectStorage
{
    private readonly ConcurrentDictionary<string, byte[]> _objects = new();
    private int _putCount;

    public int PutCount => Volatile.Read(ref _putCount);

    public Task<string> PutAsync(byte[] content, string contentType, string objectKey, CancellationToken cancellationToken)
    {
        _objects[objectKey] = content;
        Interlocked.Increment(ref _putCount);
        return Task.FromResult(objectKey);
    }

    public Task<string?> GetReadUrlAsync(string objectKey, TimeSpan ttl, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(_objects.ContainsKey(objectKey) ? $"https://cdn.example/{objectKey}" : null);
}

public sealed class InMemoryPointsLedgerRepository : IPointsLedgerRepository
{
    public ConcurrentBag<PointsLedgerEntry> Entries { get; } = new();

    public Task<(IReadOnlyList<PointsLedgerEntry> Items, bool HasMore)> ListAsync(
        ObjectId groupId,
        ObjectId userId,
        string? type,
        int limit,
        ObjectId? cursor,
        CancellationToken cancellationToken)
    {
        var items = Entries
            .Where(x => x.GroupId == groupId && x.UserId == userId)
            .Where(x => string.IsNullOrWhiteSpace(type) || x.Type == type)
            .Where(x => !cursor.HasValue || x.Id < cursor.Value)
            .OrderByDescending(x => x.Id)
            .Take(limit + 1)
            .ToList();
        return Task.FromResult<(IReadOnlyList<PointsLedgerEntry>, bool)>((items.Take(limit).ToList(), items.Count > limit));
    }
}

public sealed class InMemoryVoteRepository : IVoteRepository
{
    private readonly ConcurrentDictionary<(ObjectId, ObjectId), Vote> _votes = new();

    public Task CreateAsync(Vote vote, CancellationToken cancellationToken)
    {
        if (!_votes.TryAdd((vote.ContractId, vote.JurorId), vote))
        {
            throw Pactogg.Api.Common.ApiException.Conflict("ALREADY_VOTED", "Ya votaste en este reto.");
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Vote>> ListByContractAsync(ObjectId contractId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Vote>>(_votes.Values.Where(x => x.ContractId == contractId).OrderBy(x => x.CreatedAt).ToList());
}

public sealed class InMemoryCommitmentRepository : ICommitmentRepository
{
    private readonly ConcurrentDictionary<ObjectId, Commitment> _items = new();

    public void Add(Commitment commitment) => _items[commitment.Id] = commitment;

    public IReadOnlyList<Commitment> All => _items.Values.ToList();

    public Task<Commitment?> GetByIdAsync(ObjectId id, CancellationToken cancellationToken) =>
        Task.FromResult(_items.GetValueOrDefault(id));

    public Task<(IReadOnlyList<Commitment> Items, bool HasMore)> ListAsync(
        ObjectId groupId,
        ObjectId userId,
        bool asDebtor,
        string? status,
        int limit,
        ObjectId? cursor,
        CancellationToken cancellationToken)
    {
        var items = _items.Values
            .Where(x => x.GroupId == groupId && (asDebtor ? x.DebtorId == userId : x.CreditorId == userId))
            .Where(x => string.IsNullOrWhiteSpace(status) || x.Status == status)
            .Where(x => !cursor.HasValue || x.Id < cursor.Value)
            .OrderByDescending(x => x.Id)
            .Take(limit + 1)
            .ToList();
        return Task.FromResult<(IReadOnlyList<Commitment>, bool)>((items.Take(limit).ToList(), items.Count > limit));
    }

    public Task<Commitment?> TryTransitionAsync(ObjectId id, IReadOnlyCollection<string> fromStatuses, string toStatus, DateTime now, CancellationToken cancellationToken)
    {
        lock (_items)
        {
            if (!_items.TryGetValue(id, out var item) || !fromStatuses.Contains(item.Status))
            {
                return Task.FromResult<Commitment?>(null);
            }

            item.Status = toStatus;
            item.UpdatedAt = now;
            if (toStatus == "completed")
            {
                item.CompletedAt = now;
            }
            else if (toStatus == "completion_requested")
            {
                item.RequestedAt = now;
            }

            return Task.FromResult<Commitment?>(item);
        }
    }
}

public sealed class InMemoryNotificationRepository : INotificationRepository
{
    private readonly ConcurrentDictionary<ObjectId, Notification> _items = new();

    public IReadOnlyList<Notification> For(ObjectId userId) => _items.Values.Where(x => x.UserId == userId).ToList();

    public Task<IReadOnlyList<Notification>> CreateManyAsync(IReadOnlyList<Notification> notifications, CancellationToken cancellationToken)
    {
        var inserted = new List<Notification>();
        lock (_items)
        {
            foreach (var notification in notifications)
            {
                if (notification.DedupeKey is not null && _items.Values.Any(x => x.DedupeKey == notification.DedupeKey))
                {
                    continue;
                }

                _items[notification.Id] = notification;
                inserted.Add(notification);
            }
        }

        return Task.FromResult<IReadOnlyList<Notification>>(inserted);
    }

    public Task<(IReadOnlyList<Notification> Items, bool HasMore)> ListAsync(ObjectId userId, bool unreadOnly, int limit, ObjectId? cursor, CancellationToken cancellationToken)
    {
        var items = _items.Values
            .Where(x => x.UserId == userId && (!unreadOnly || x.ReadAt is null))
            .Where(x => !cursor.HasValue || x.Id < cursor.Value)
            .OrderByDescending(x => x.Id)
            .Take(limit + 1)
            .ToList();
        return Task.FromResult<(IReadOnlyList<Notification>, bool)>((items.Take(limit).ToList(), items.Count > limit));
    }

    public Task<long> CountUnreadAsync(ObjectId userId, CancellationToken cancellationToken) =>
        Task.FromResult((long)_items.Values.Count(x => x.UserId == userId && x.ReadAt is null));

    public Task<bool> DeleteAsync(ObjectId id, ObjectId userId, CancellationToken cancellationToken)
    {
        lock (_items)
        {
            return Task.FromResult(_items.TryGetValue(id, out var item) && item.UserId == userId && _items.TryRemove(id, out _));
        }
    }

    public Task<Notification?> MarkReadAsync(ObjectId id, ObjectId userId, DateTime at, CancellationToken cancellationToken)
    {
        if (!_items.TryGetValue(id, out var item) || item.UserId != userId)
        {
            return Task.FromResult<Notification?>(null);
        }

        item.ReadAt ??= at;
        return Task.FromResult<Notification?>(item);
    }

    public Task<long> MarkAllReadAsync(ObjectId userId, DateTime at, CancellationToken cancellationToken)
    {
        long count = 0;
        foreach (var item in _items.Values.Where(x => x.UserId == userId && x.ReadAt is null))
        {
            item.ReadAt = at;
            count++;
        }

        return Task.FromResult(count);
    }
}

/// <summary>Token de prueba: "google|sub|correo|verified" (verified = true/false).</summary>
public sealed class FakeGoogleTokenValidator : Pactogg.Api.Infrastructure.Security.IGoogleTokenValidator
{
    public Task<Pactogg.Api.Infrastructure.Security.GoogleIdentity?> ValidateAsync(string idToken, CancellationToken cancellationToken)
    {
        var parts = idToken.Split('|');
        if (parts.Length != 4 || parts[0] != "google")
        {
            return Task.FromResult<Pactogg.Api.Infrastructure.Security.GoogleIdentity?>(null);
        }

        return Task.FromResult<Pactogg.Api.Infrastructure.Security.GoogleIdentity?>(
            new Pactogg.Api.Infrastructure.Security.GoogleIdentity(parts[1], parts[2], parts[3] == "true", "Jugador"));
    }
}

public sealed class InMemoryDeviceRepository : Pactogg.Api.Modules.Devices.IDeviceRepository
{
    private readonly ConcurrentDictionary<string, Pactogg.Api.Modules.Devices.Device> _items = new();

    public Task<Pactogg.Api.Modules.Devices.Device> UpsertAsync(ObjectId userId, string fcmToken, string platform, string? appVersion, DateTime now, CancellationToken cancellationToken)
    {
        var device = _items.AddOrUpdate(
            fcmToken,
            _ => new Pactogg.Api.Modules.Devices.Device { Id = ObjectId.GenerateNewId(), UserId = userId, FcmToken = fcmToken, Platform = platform, AppVersion = appVersion, CreatedAt = now, UpdatedAt = now },
            (_, existing) =>
            {
                existing.UserId = userId;
                existing.Platform = platform;
                existing.AppVersion = appVersion;
                existing.UpdatedAt = now;
                return existing;
            });
        return Task.FromResult(device);
    }

    public Task<long> DeleteAsync(ObjectId userId, string? fcmToken, CancellationToken cancellationToken)
    {
        var removed = 0L;
        foreach (var item in _items.Values.Where(x => x.UserId == userId && (fcmToken == null || x.FcmToken == fcmToken)).ToList())
        {
            if (_items.TryRemove(item.FcmToken, out _))
            {
                removed++;
            }
        }

        return Task.FromResult(removed);
    }

    public Task<IReadOnlyList<Pactogg.Api.Modules.Devices.Device>> ListByUsersAsync(IReadOnlyCollection<ObjectId> userIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Pactogg.Api.Modules.Devices.Device>>(_items.Values.Where(x => userIds.Contains(x.UserId)).ToList());

    public Task DeleteTokensAsync(IReadOnlyCollection<string> fcmTokens, CancellationToken cancellationToken)
    {
        foreach (var token in fcmTokens)
        {
            _items.TryRemove(token, out _);
        }

        return Task.CompletedTask;
    }
}

/// <summary>Guarda los correos en memoria para que las pruebas lean el token enviado.</summary>
public sealed class FakeEmailSender : Pactogg.Api.Infrastructure.Email.IEmailSender
{
    public ConcurrentQueue<Pactogg.Api.Infrastructure.Email.EmailMessage> Sent { get; } = new();

    public Task<bool> SendAsync(Pactogg.Api.Infrastructure.Email.EmailMessage message, CancellationToken cancellationToken)
    {
        Sent.Enqueue(message);
        return Task.FromResult(true);
    }

    /// <summary>Último token enviado a ese correo (la línea "Código: ..." del texto plano).</summary>
    public string? LastTokenFor(string email, string subjectContains) =>
        Sent.Reverse()
            .Where(x => string.Equals(x.To, email, StringComparison.OrdinalIgnoreCase) && x.Subject.Contains(subjectContains, StringComparison.Ordinal))
            .Select(x => x.TextBody.Split('\n').FirstOrDefault(line => line.StartsWith("Código: ", StringComparison.Ordinal))?["Código: ".Length..].Trim())
            .FirstOrDefault();
}

public sealed class FakePushSender : Pactogg.Api.Infrastructure.Push.IPushSender
{
    public ConcurrentQueue<(string Token, Pactogg.Api.Infrastructure.Push.PushMessage Message)> Sent { get; } = new();
    public HashSet<string> Unregistered { get; } = [];

    public bool IsConfigured => true;

    public Task<Pactogg.Api.Infrastructure.Push.PushResult> SendAsync(IReadOnlyCollection<string> tokens, Pactogg.Api.Infrastructure.Push.PushMessage message, CancellationToken cancellationToken)
    {
        var invalid = tokens.Where(Unregistered.Contains).ToList();
        foreach (var token in tokens.Except(invalid))
        {
            Sent.Enqueue((token, message));
        }

        return Task.FromResult(new Pactogg.Api.Infrastructure.Push.PushResult(tokens.Count - invalid.Count, invalid));
    }
}
