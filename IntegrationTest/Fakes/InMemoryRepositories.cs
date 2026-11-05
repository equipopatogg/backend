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

    public Task<IReadOnlyList<User>> ListByIdsAsync(IReadOnlyCollection<ObjectId> ids, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<User>>(_users.Values.Where(x => ids.Contains(x.Id)).ToList());

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
            var receiver = fulfilled ? acceptor : contract.ChallengerId;
            var member = _members.Find(contract.GroupId, receiver);
            if (member is not null)
            {
                member.PointsBalance += contract.BondRequired;
                _ledger.Entries.Add(new PointsLedgerEntry
                {
                    Id = ObjectId.GenerateNewId(),
                    GroupId = contract.GroupId,
                    UserId = receiver,
                    Type = fulfilled ? "bond_release" : "bond_forfeit",
                    Amount = contract.BondRequired,
                    RefType = "contract",
                    RefId = contract.Id,
                    BalanceAfter = member.PointsBalance,
                    CreatedAt = now
                });
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

public sealed class FakeAuditService : IAuditService
{
    public Task AppendAsync(
        string action,
        ObjectId? actorId,
        string entityType,
        ObjectId? entityId,
        ObjectId? groupId,
        object? payload,
        CancellationToken cancellationToken) => Task.CompletedTask;
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

    public Task CreateManyAsync(IReadOnlyCollection<Notification> notifications, CancellationToken cancellationToken)
    {
        foreach (var notification in notifications)
        {
            _items[notification.Id] = notification;
        }

        return Task.CompletedTask;
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
