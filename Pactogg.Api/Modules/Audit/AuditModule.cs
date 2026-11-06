using System.Text.Json;
using MongoDB.Bson;
using Pactogg.Api.Common;
using Pactogg.Api.Common.Extensions;
using Pactogg.Api.Infrastructure.Audit;
using Pactogg.Api.Modules.Auth.Abstractions;
using Pactogg.Api.Modules.Groups;

namespace Pactogg.Api.Modules.Audit;

public sealed class AuditEventResponse
{
    public string Id { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string? ActorId { get; init; }
    public string? ActorNickname { get; init; }
    public string EntityType { get; init; } = string.Empty;
    public string? EntityId { get; init; }
    public JsonElement Payload { get; init; }
    public string Hash { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// AUD-01 (ID 160): historial inmutable del grupo, solo para administradores. Cada evento lleva el hash
/// encadenado con el anterior, así que una alteración se detecta. AUD-02: no hay endpoints para cambiarlo.
/// </summary>
public sealed class AuditQueryService
{
    private static readonly HashSet<string> EntityTypes = ["contract", "group", "member", "commitment", "evidence", "vote", "tournament"];

    private readonly IAuditReader _reader;
    private readonly GroupAccess _access;
    private readonly IUserRepository _users;

    public AuditQueryService(IAuditReader reader, GroupAccess access, IUserRepository users)
    {
        _reader = reader;
        _access = access;
        _users = users;
    }

    public async Task<(IReadOnlyList<AuditEventResponse> Items, object Meta)> ListAsync(
        ObjectId userId,
        string gid,
        string? entity,
        string? entityId,
        int limit,
        string? cursor,
        CancellationToken cancellationToken)
    {
        ListQuery.ValidateLimit(limit);
        var cursorId = ListQuery.ParseCursor(cursor);
        var (group, member) = await _access.RequireMemberAsync(userId, gid, cancellationToken);
        if (member.Role != "admin")
        {
            throw ApiException.Forbidden(ErrorCodes.NotGroupAdmin, "Solo los administradores ven el historial del grupo.");
        }

        if (entity is not null && !EntityTypes.Contains(entity))
        {
            throw ApiException.Unprocessable(ErrorCodes.ValidationError, $"entity debe ser uno de: {string.Join(", ", EntityTypes)}.");
        }

        ObjectId? entityObjectId = null;
        if (!string.IsNullOrWhiteSpace(entityId))
        {
            if (!ObjectId.TryParse(entityId, out var parsed))
            {
                throw ApiException.Unprocessable(ErrorCodes.ValidationError, "entity_id no es válido.");
            }

            entityObjectId = parsed;
        }

        var (items, hasMore) = await _reader.ListByGroupAsync(group.Id, entity, entityObjectId, limit, cursorId, cancellationToken);
        var actors = (await _users.ListByIdsAsync(items.Where(x => x.ActorId is not null).Select(x => x.ActorId!.Value).Distinct().ToArray(), cancellationToken))
            .ToDictionary(x => x.Id, x => x.Nickname);
        var responses = items.Select(x => new AuditEventResponse
        {
            Id = x.Id.ToString(),
            Action = x.Action,
            ActorId = x.ActorId?.ToString(),
            ActorNickname = x.ActorId is ObjectId actor && actors.TryGetValue(actor, out var nick) ? nick : x.ActorId is null ? "sistema" : null,
            EntityType = x.EntityType,
            EntityId = x.EntityId?.ToString(),
            Payload = ParsePayload(x.Payload),
            Hash = x.Hash,
            CreatedAt = x.CreatedAt
        }).ToList();
        var next = hasMore && items.Count > 0 ? items[^1].Id.ToString() : null;
        return (responses, new { next_cursor = next, has_more = hasMore });
    }

    private static JsonElement ParsePayload(string payload)
    {
        try
        {
            return JsonDocument.Parse(string.IsNullOrWhiteSpace(payload) ? "{}" : payload).RootElement.Clone();
        }
        catch (JsonException)
        {
            return JsonDocument.Parse("{}").RootElement.Clone();
        }
    }
}

public static class AuditEndpoints
{
    public static IEndpointRouteBuilder MapAuditEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/groups/{gid}/audit", async (
                string gid,
                string? entity,
                string? entity_id,
                int? limit,
                string? cursor,
                AuditQueryService service,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var (items, meta) = await service.ListAsync(context.User.GetRequiredObjectId(), gid, entity, entity_id, limit ?? 20, cursor, cancellationToken);
                return ApiResults.Ok(items, meta);
            })
            .WithTags("Audit")
            .RequireAuthorization()
            .WithName("ListGroupAudit")
            .Produces<IReadOnlyList<AuditEventResponse>>()
            .ProducesProblem(StatusCodes.Status403Forbidden);
        return app;
    }
}
