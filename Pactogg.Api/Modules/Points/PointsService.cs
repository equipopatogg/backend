using MongoDB.Bson;
using Pactogg.Api.Common;
using Pactogg.Api.Infrastructure.Redis;
using Pactogg.Api.Modules.Auth.Abstractions;
using Pactogg.Api.Modules.Groups;
using Pactogg.Api.Modules.Groups.Abstractions;
using Pactogg.Api.Modules.Points.Abstractions;
using Pactogg.Api.Modules.Points.Dtos;

namespace Pactogg.Api.Modules.Points;

/// <summary>PTS-01 y PTS-02. Los puntos no tienen valor monetario y no existe ningún endpoint para comprarlos.</summary>
public sealed class PointsService
{
    private static readonly HashSet<string> ValidTypes = new(StringComparer.Ordinal)
    {
        "bond_lock",
        "bond_release",
        "bond_forfeit",
        "reward",
        "tournament_entry",
        "tournament_prize",
        "adjustment"
    };

    private readonly IPointsLedgerRepository _ledger;
    private readonly IGroupMemberRepository _members;
    private readonly IUserRepository _users;
    private readonly GroupAccess _access;
    private readonly ICacheService _cache;

    public PointsService(
        IPointsLedgerRepository ledger,
        IGroupMemberRepository members,
        IUserRepository users,
        GroupAccess access,
        ICacheService cache)
    {
        _ledger = ledger;
        _members = members;
        _users = users;
        _access = access;
        _cache = cache;
    }

    public async Task<(MyPointsResponse Data, object Meta)> GetMineAsync(
        ObjectId userId,
        string gid,
        string? type,
        int limit,
        string? cursor,
        CancellationToken cancellationToken)
    {
        ListQuery.ValidateLimit(limit);
        var cursorId = ListQuery.ParseCursor(cursor);
        if (!string.IsNullOrWhiteSpace(type) && !ValidTypes.Contains(type))
        {
            throw ApiException.Unprocessable(ErrorCodes.ValidationError, "type no es válido.");
        }

        var (group, member) = await _access.RequireMemberAsync(userId, gid, cancellationToken);
        var (items, hasMore) = await _ledger.ListAsync(group.Id, userId, type, limit, cursorId, cancellationToken);
        var next = hasMore && items.Count > 0 ? items[^1].Id.ToString() : null;
        var data = new MyPointsResponse
        {
            GroupId = group.Id.ToString(),
            Balance = member.PointsBalance,
            Movements = items.Select(x => new PointsMovementResponse
            {
                Id = x.Id.ToString(),
                Type = x.Type,
                Amount = x.Amount,
                BalanceAfter = x.BalanceAfter,
                RefType = x.RefType,
                RefId = x.RefId.ToString(),
                CreatedAt = x.CreatedAt
            }).ToList()
        };
        return (data, new { next_cursor = next, has_more = hasMore });
    }

    /// <summary>PTS-03: detalle de un movimiento; solo el dueño o un admin de su grupo.</summary>
    public async Task<PointsMovementResponse> GetMovementAsync(ObjectId userId, string id, CancellationToken cancellationToken)
    {
        var entry = ObjectId.TryParse(id, out var entryId) ? await _ledger.GetByIdAsync(entryId, cancellationToken) : null;
        if (entry is null)
        {
            throw ApiException.NotFound("El movimiento no existe.");
        }

        var (_, member) = await _access.RequireMemberAsync(userId, entry.GroupId, cancellationToken);
        if (entry.UserId != userId && member.Role != "admin")
        {
            throw ApiException.NotFound("El movimiento no existe.");
        }

        return new PointsMovementResponse
        {
            Id = entry.Id.ToString(),
            Type = entry.Type,
            Amount = entry.Amount,
            BalanceAfter = entry.BalanceAfter,
            RefType = entry.RefType,
            RefId = entry.RefId.ToString(),
            CreatedAt = entry.CreatedAt
        };
    }

    /// <summary>ACH-03: ranking del grupo por puntos (sin temporadas en la primera versión, CLARIFICACION #113) y Rey del Grupo.</summary>
    public async Task<GroupRankingResponse> GroupRankingAsync(ObjectId userId, string gid, int limit, CancellationToken cancellationToken)
    {
        var items = await RankingAsync(userId, gid, limit, cancellationToken);
        var king = items.FirstOrDefault(x => x.PointsBalance > 0 && (items.Count == 1 || items[1].PointsBalance < x.PointsBalance));
        return new GroupRankingResponse { King = king, Items = items };
    }

    public async Task<IReadOnlyList<RankingEntryResponse>> RankingAsync(ObjectId userId, string gid, int limit, CancellationToken cancellationToken)
    {
        ListQuery.ValidateLimit(limit);
        var (group, _) = await _access.RequireMemberAsync(userId, gid, cancellationToken);
        var version = await _cache.GetVersionAsync(CacheExtensions.GroupScope(group.Id), cancellationToken);
        return await _cache.GetOrCreateJsonAsync<IReadOnlyList<RankingEntryResponse>>(
            $"points:ranking:{group.Id}:{version}:{limit}",
            TimeSpan.FromSeconds(60),
            async () =>
            {
                var members = await _members.ListRankingAsync(group.Id, limit, cancellationToken);
                var users = (await _users.ListByIdsAsync(members.Select(x => x.UserId).ToArray(), cancellationToken)).ToDictionary(x => x.Id);
                return members.Select((member, index) => new RankingEntryResponse
                {
                    Position = index + 1,
                    UserId = member.UserId.ToString(),
                    Nickname = users.TryGetValue(member.UserId, out var user) ? user.Nickname : member.NickLower,
                    AvatarId = user?.AvatarId ?? "default",
                    PointsBalance = member.PointsBalance
                }).ToList();
            },
            cancellationToken);
    }
}
