using System.Text.Json;
using MongoDB.Bson;
using Pactogg.Api.Common;
using Pactogg.Api.Infrastructure.Redis;
using Pactogg.Api.Modules.Groups.Abstractions;
using Pactogg.Api.Modules.Tournaments.Abstractions;
using Pactogg.Api.Modules.Tournaments.Domain;
using Pactogg.Api.Modules.Tournaments.Dtos;

namespace Pactogg.Api.Modules.Tournaments;

public sealed class TournamentService
{
    private static readonly HashSet<string> ValidStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "open",
        "in_progress",
        "finished",
        "cancelled"
    };

    private readonly ITournamentRepository _tournaments;
    private readonly IMatchRepository _matches;
    private readonly IGroupMemberRepository _members;
    private readonly ICacheService _cache;

    public TournamentService(
        ITournamentRepository tournaments,
        IMatchRepository matches,
        IGroupMemberRepository members,
        ICacheService cache)
    {
        _tournaments = tournaments;
        _matches = matches;
        _members = members;
        _cache = cache;
    }

    public async Task<(IReadOnlyList<TournamentResponse> Items, object Meta)> ListAsync(
        ObjectId userId,
        string? status,
        string? game,
        string? groupId,
        int limit,
        string? cursor,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(status) && !ValidStatuses.Contains(status))
        {
            throw ApiException.Unprocessable(ErrorCodes.ValidationError, "status no es válido.");
        }

        if (limit is < 1 or > 50)
        {
            throw ApiException.Unprocessable(ErrorCodes.ValidationError, "limit debe estar entre 1 y 50.");
        }

        if (!string.IsNullOrWhiteSpace(game) && game.Length > 60)
        {
            throw ApiException.Unprocessable(ErrorCodes.ValidationError, "game es demasiado largo.");
        }

        ObjectId? groupFilter = null;
        if (!string.IsNullOrWhiteSpace(groupId))
        {
            if (!ObjectId.TryParse(groupId, out var parsedGroup))
            {
                throw ApiException.Unprocessable(ErrorCodes.ValidationError, "group_id no es válido.");
            }

            if (await _members.GetAsync(parsedGroup, userId, cancellationToken) is null)
            {
                throw ApiException.Forbidden(ErrorCodes.NotGroupMember, "No perteneces a este grupo.");
            }

            groupFilter = parsedGroup;
        }

        ObjectId? cursorId = null;
        if (!string.IsNullOrWhiteSpace(cursor))
        {
            if (!ObjectId.TryParse(cursor, out var parsedCursor))
            {
                throw ApiException.Unprocessable(ErrorCodes.ValidationError, "cursor no es válido.");
            }

            cursorId = parsedCursor;
        }

        var cacheKey = $"tournaments:{userId}:{status}:{game}:{groupId}:{limit}:{cursor}";
        var cached = await _cache.GetAsync(cacheKey, cancellationToken);
        if (!string.IsNullOrWhiteSpace(cached))
        {
            var snapshot = JsonSerializer.Deserialize<TournamentListSnapshot>(cached, ApiResults.JsonOptions);
            if (snapshot is not null)
            {
                return (snapshot.Items, new { next_cursor = snapshot.NextCursor, has_more = snapshot.HasMore });
            }
        }

        var groupIds = await _members.ListActiveGroupIdsAsync(userId, cancellationToken);
        if (groupIds.Count == 0)
        {
            await CacheAsync(cacheKey, [], false, cancellationToken);
            return ([], new { next_cursor = (string?)null, has_more = false });
        }

        var (items, hasMore) = await _tournaments.ListForGroupsAsync(
            groupIds,
            status,
            game,
            groupFilter,
            limit,
            cursorId,
            cancellationToken);

        var matches = await _matches.ListForTournamentIdsAsync(items.Select(x => x.Id).ToArray(), cancellationToken);
        var byTournament = matches.GroupBy(x => x.TournamentId).ToDictionary(x => x.Key, x => x.ToList());

        var responses = items.Select(item => ToResponse(item, byTournament.GetValueOrDefault(item.Id, []))).ToList();
        var nextCursor = hasMore && items.Count > 0 ? items[^1].Id.ToString() : null;
        await CacheAsync(cacheKey, responses, hasMore, cancellationToken, nextCursor);
        return (responses, new { next_cursor = nextCursor, has_more = hasMore });
    }

    private async Task CacheAsync(
        string key,
        IReadOnlyList<TournamentResponse> items,
        bool hasMore,
        CancellationToken cancellationToken,
        string? nextCursor = null)
    {
        var snapshot = new TournamentListSnapshot(items, nextCursor, hasMore);
        await _cache.SetAsync(key, JsonSerializer.Serialize(snapshot, ApiResults.JsonOptions), TimeSpan.FromSeconds(30), cancellationToken);
    }

    private static TournamentResponse ToResponse(Tournament tournament, IReadOnlyList<Match> matches) => new()
    {
        Id = tournament.Id.ToString(),
        GroupId = tournament.GroupId.ToString(),
        Game = tournament.Game,
        Format = tournament.Format,
        EntryPoints = tournament.EntryPoints,
        PrizePool = tournament.PrizePool,
        ScheduledAt = tournament.ScheduledAt,
        Participants = tournament.Participants.Select(x => x.ToString()).ToList(),
        Status = tournament.Status,
        WinnerGroupId = tournament.WinnerGroupId?.ToString(),
        Bracket = new BracketResponse
        {
            Matches = matches.Select(match => new MatchResponse
            {
                Id = match.Id.ToString(),
                Round = match.Round,
                Position = match.Position,
                P1GroupId = match.P1GroupId?.ToString(),
                P2GroupId = match.P2GroupId?.ToString(),
                P1Score = match.P1Score,
                P2Score = match.P2Score,
                WinnerGroupId = match.WinnerGroupId?.ToString(),
                Status = match.Status
            }).ToList()
        }
    };
}

internal sealed record TournamentListSnapshot(
    IReadOnlyList<TournamentResponse> Items,
    string? NextCursor,
    bool HasMore);
