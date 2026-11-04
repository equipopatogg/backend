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

    public Task CreateAsync(GroupMember member, CancellationToken cancellationToken)
    {
        _members[member.Id] = member;
        return Task.CompletedTask;
    }
}

public sealed class InMemoryContractRepository : IContractRepository
{
    private readonly ConcurrentDictionary<ObjectId, Contract> _contracts = new();

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

    public Task MarkVotingAsync(ObjectId id, CancellationToken cancellationToken)
    {
        if (_contracts.TryGetValue(id, out var contract))
        {
            contract.Status = "voting";
            contract.UpdatedAt = DateTime.UtcNow;
        }

        return Task.CompletedTask;
    }
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
