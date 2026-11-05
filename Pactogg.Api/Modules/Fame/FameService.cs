using MongoDB.Bson;
using Pactogg.Api.Common;
using Pactogg.Api.Infrastructure.Redis;
using Pactogg.Api.Infrastructure.Storage;
using Pactogg.Api.Modules.Auth.Abstractions;
using Pactogg.Api.Modules.Contracts.Abstractions;
using Pactogg.Api.Modules.Evidence.Abstractions;
using Pactogg.Api.Modules.Groups;

namespace Pactogg.Api.Modules.Fame;

public sealed class FameExecutorResponse
{
    public string UserId { get; init; } = string.Empty;
    public string Nickname { get; init; } = string.Empty;
    public string AvatarId { get; init; } = "default";
}

public sealed class FameEntryResponse
{
    public string ContractId { get; init; } = string.Empty;
    public FameExecutorResponse? Executor { get; init; }
    public string Game { get; init; } = string.Empty;
    public string Condition { get; init; } = string.Empty;
    public string Reward { get; init; } = string.Empty;
    public DateTime? ResolvedAt { get; init; }
    public IReadOnlyList<string> Photos { get; init; } = [];
    public IReadOnlyList<string> Links { get; init; } = [];
}

/// <summary>ACH-01: Muro de la Fama = retos cumplidos del grupo con sus fotos y clips aprobados.</summary>
public sealed class FameService
{
    private readonly IContractRepository _contracts;
    private readonly IEvidenceRepository _evidence;
    private readonly IUserRepository _users;
    private readonly IObjectStorage _storage;
    private readonly GroupAccess _access;
    private readonly ICacheService _cache;

    public FameService(
        IContractRepository contracts,
        IEvidenceRepository evidence,
        IUserRepository users,
        IObjectStorage storage,
        GroupAccess access,
        ICacheService cache)
    {
        _contracts = contracts;
        _evidence = evidence;
        _users = users;
        _storage = storage;
        _access = access;
        _cache = cache;
    }

    public async Task<(IReadOnlyList<FameEntryResponse> Items, object Meta)> ListAsync(
        ObjectId userId,
        string gid,
        int limit,
        string? cursor,
        CancellationToken cancellationToken)
    {
        ListQuery.ValidateLimit(limit);
        var cursorId = ListQuery.ParseCursor(cursor);
        var (group, _) = await _access.RequireMemberAsync(userId, gid, cancellationToken);
        var version = await _cache.GetVersionAsync(CacheExtensions.GroupScope(group.Id), cancellationToken);
        var snapshot = await _cache.GetOrCreateJsonAsync(
            $"fame:{group.Id}:{version}:{limit}:{cursor}",
            TimeSpan.FromSeconds(60),
            async () =>
            {
                var (items, hasMore) = await _contracts.ListFulfilledAsync(group.Id, limit, cursorId, cancellationToken);
                var evidence = await _evidence.ListByContractIdsAsync(items.Select(x => x.Id).ToArray(), cancellationToken);
                var byContract = evidence.Where(x => x.Moderation.Status == "approved").ToLookup(x => x.ContractId);
                var executors = items.Select(x => x.ExecutorId ?? x.AcceptedBy ?? x.TargetId).OfType<ObjectId>().Distinct().ToArray();
                var users = (await _users.ListByIdsAsync(executors, cancellationToken)).ToDictionary(x => x.Id);

                var entries = new List<FameEntryResponse>(items.Count);
                foreach (var contract in items)
                {
                    var photos = new List<string>();
                    foreach (var photo in byContract[contract.Id].Where(x => x.Type == "photo" && !string.IsNullOrWhiteSpace(x.ObjectKey)))
                    {
                        var url = await _storage.GetReadUrlAsync(photo.ObjectKey!, TimeSpan.FromHours(1), cancellationToken);
                        if (url is not null)
                        {
                            photos.Add(url);
                        }
                    }

                    var executorId = contract.ExecutorId ?? contract.AcceptedBy ?? contract.TargetId;
                    entries.Add(new FameEntryResponse
                    {
                        ContractId = contract.Id.ToString(),
                        Executor = executorId is ObjectId id && users.TryGetValue(id, out var user)
                            ? new FameExecutorResponse { UserId = id.ToString(), Nickname = user.Nickname, AvatarId = user.AvatarId }
                            : null,
                        Game = contract.Game,
                        Condition = contract.Condition,
                        Reward = contract.Reward,
                        ResolvedAt = contract.ResolvedAt,
                        Photos = photos,
                        Links = byContract[contract.Id].Where(x => x.Type == "link" && x.Url is not null).Select(x => x.Url!).ToList()
                    });
                }

                var next = hasMore && items.Count > 0 ? items[^1].Id.ToString() : null;
                return new PageSnapshot<FameEntryResponse>(entries, next, hasMore);
            },
            cancellationToken);

        return (snapshot.Items, snapshot.Meta);
    }
}

public static class FameEndpoints
{
    public static IEndpointRouteBuilder MapFameEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/groups/{gid}/fame", async (
                string gid,
                int? limit,
                string? cursor,
                FameService service,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var (items, meta) = await service.ListAsync(Common.Extensions.ClaimsPrincipalExtensions.GetRequiredObjectId(context.User), gid, limit ?? 20, cursor, cancellationToken);
                return ApiResults.Ok(items, meta);
            })
            .RequireAuthorization()
            .WithTags("Fame")
            .WithName("GetHallOfFame")
            .Produces<IReadOnlyList<FameEntryResponse>>()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return app;
    }
}
