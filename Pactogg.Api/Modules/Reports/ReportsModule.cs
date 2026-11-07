using System.ComponentModel.DataAnnotations;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Pactogg.Api.Common;
using Pactogg.Api.Common.Extensions;
using Pactogg.Api.Infrastructure.Audit;
using Pactogg.Api.Infrastructure.Redis;
using Pactogg.Api.Infrastructure.Storage;
using Pactogg.Api.Modules.Auth.Abstractions;
using Pactogg.Api.Modules.Contracts.Abstractions;
using Pactogg.Api.Modules.Evidence.Abstractions;
using Pactogg.Api.Modules.Groups;
using Pactogg.Api.Modules.Votes.Abstractions;

namespace Pactogg.Api.Modules.Reports;

[BsonIgnoreExtraElements]
public sealed class Report
{
    [BsonId] public ObjectId Id { get; set; }
    [BsonElement("group_id")] public ObjectId GroupId { get; set; }
    [BsonElement("reporter_id")] public ObjectId ReporterId { get; set; }
    /// <summary>evidence, comment o user.</summary>
    [BsonElement("target_type")] public string TargetType { get; set; } = string.Empty;
    [BsonElement("target_id")] public ObjectId TargetId { get; set; }
    [BsonElement("reason")] public string Reason { get; set; } = string.Empty;
    [BsonElement("details")] public string? Details { get; set; }
    /// <summary>open, dismissed, content_removed o escalated (cuentas: las revisa el administrador de la plataforma).</summary>
    [BsonElement("status")] public string Status { get; set; } = "open";
    [BsonElement("resolved_by")] public ObjectId? ResolvedBy { get; set; }
    [BsonElement("resolved_at")] public DateTime? ResolvedAt { get; set; }
    [BsonElement("created_at")] public DateTime CreatedAt { get; set; }
}

[BsonIgnoreExtraElements]
public sealed class UserBlock
{
    [BsonId] public ObjectId Id { get; set; }
    [BsonElement("blocker_id")] public ObjectId BlockerId { get; set; }
    [BsonElement("blocked_id")] public ObjectId BlockedId { get; set; }
    [BsonElement("created_at")] public DateTime CreatedAt { get; set; }
}

public interface IReportRepository
{
    /// <summary>false si esa persona ya reportó ese objetivo.</summary>
    Task<bool> TryCreateAsync(Report report, CancellationToken cancellationToken);
    Task<Report?> GetByIdAsync(ObjectId id, CancellationToken cancellationToken);
    Task<(IReadOnlyList<Report> Items, bool HasMore)> ListByGroupAsync(ObjectId groupId, string? status, int limit, ObjectId? cursor, CancellationToken cancellationToken);
    Task<Report?> TryResolveAsync(ObjectId id, string status, ObjectId by, DateTime now, CancellationToken cancellationToken);
    /// <summary>Cierra los demás reportes abiertos del mismo objetivo.</summary>
    Task ResolveSameTargetAsync(ObjectId groupId, string targetType, ObjectId targetId, string status, ObjectId by, DateTime now, CancellationToken cancellationToken);
}

public interface IBlockRepository
{
    Task<bool> TryBlockAsync(UserBlock block, CancellationToken cancellationToken);
    Task<bool> UnblockAsync(ObjectId blockerId, ObjectId blockedId, CancellationToken cancellationToken);
    Task<bool> IsBlockedAsync(ObjectId blockerId, ObjectId blockedId, CancellationToken cancellationToken);
    Task DeleteAllForUserAsync(ObjectId userId, CancellationToken cancellationToken);
}

public sealed class CreateReportRequest
{
    [Required] public string GroupId { get; set; } = string.Empty;
    [Required, RegularExpression("^(evidence|comment|user)$", ErrorMessage = "target_type debe ser evidence, comment o user.")]
    public string TargetType { get; set; } = string.Empty;
    [Required] public string TargetId { get; set; } = string.Empty;
    [Required, RegularExpression("^(ofensivo|sexual|odio|spam|trampa|otro)$", ErrorMessage = "reason no es válido.")]
    public string Reason { get; set; } = string.Empty;
    [StringLength(280)] public string? Details { get; set; }
}

public sealed class ResolveReportRequest
{
    [Required, RegularExpression("^(dismiss|remove_content)$", ErrorMessage = "action debe ser dismiss o remove_content.")]
    public string Action { get; set; } = string.Empty;
}

public sealed class ReportResponse
{
    public string Id { get; init; } = string.Empty;
    public string GroupId { get; init; } = string.Empty;
    public string ReporterId { get; init; } = string.Empty;
    public string TargetType { get; init; } = string.Empty;
    public string TargetId { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public string? Details { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime? ResolvedAt { get; init; }
}

/// <summary>REP-01..06: reportes de contenido, eliminación por el administrador del grupo y bloqueos.</summary>
public sealed class ReportService
{
    private readonly IReportRepository _reports;
    private readonly IBlockRepository _blocks;
    private readonly IEvidenceRepository _evidence;
    private readonly IVoteRepository _votes;
    private readonly IContractRepository _contracts;
    private readonly IUserRepository _users;
    private readonly IObjectStorage _storage;
    private readonly GroupAccess _access;
    private readonly IAuditService _audit;
    private readonly ICacheService _cache;

    public ReportService(IReportRepository reports, IBlockRepository blocks, IEvidenceRepository evidence, IVoteRepository votes,
        IContractRepository contracts, IUserRepository users, IObjectStorage storage, GroupAccess access, IAuditService audit, ICacheService cache)
    {
        _reports = reports;
        _blocks = blocks;
        _evidence = evidence;
        _votes = votes;
        _contracts = contracts;
        _users = users;
        _storage = storage;
        _access = access;
        _audit = audit;
        _cache = cache;
    }

    /// <summary>REP-01: el objetivo debe pertenecer al grupo indicado (anti-IDOR). Uno por persona y objetivo.</summary>
    public async Task<ReportResponse> CreateAsync(ObjectId userId, CreateReportRequest request, CancellationToken cancellationToken)
    {
        var (group, _) = await _access.RequireMemberAsync(userId, request.GroupId, cancellationToken);
        if (!ObjectId.TryParse(request.TargetId, out var targetId) || !await TargetBelongsAsync(group.Id, request.TargetType, targetId, cancellationToken))
        {
            throw ApiException.NotFound("No existe ese contenido en el grupo.");
        }

        if (request.TargetType == "user" && targetId == userId)
        {
            throw ApiException.Unprocessable(ErrorCodes.ValidationError, "No puedes reportarte a ti mismo.");
        }

        var report = new Report
        {
            Id = ObjectId.GenerateNewId(),
            GroupId = group.Id,
            ReporterId = userId,
            TargetType = request.TargetType,
            TargetId = targetId,
            Reason = request.Reason,
            Details = string.IsNullOrWhiteSpace(request.Details) ? null : request.Details.Trim(),
            // CLARIFICACION #19: las cuentas las revisa el administrador de la plataforma.
            Status = request.TargetType == "user" ? "escalated" : "open",
            CreatedAt = DateTime.UtcNow
        };

        if (!await _reports.TryCreateAsync(report, cancellationToken))
        {
            throw ApiException.Conflict(ErrorCodes.AlreadyReported, "Ya reportaste este contenido.");
        }

        await _audit.AppendAsync("report.created", userId, "report", report.Id, group.Id, new { target_type = report.TargetType, reason = report.Reason }, cancellationToken);
        return ToResponse(report);
    }

    /// <summary>REP-02: solo administradores del grupo.</summary>
    public async Task<(IReadOnlyList<ReportResponse> Items, object Meta)> ListAsync(ObjectId userId, string gid, string? status, int limit, string? cursor, CancellationToken cancellationToken)
    {
        ListQuery.ValidateLimit(limit);
        var cursorId = ListQuery.ParseCursor(cursor);
        if (status is not null && status is not ("open" or "dismissed" or "content_removed" or "escalated"))
        {
            throw ApiException.Unprocessable(ErrorCodes.ValidationError, "status no es válido.");
        }

        var (group, member) = await _access.RequireMemberAsync(userId, gid, cancellationToken);
        RequireAdmin(member.Role);
        var (items, hasMore) = await _reports.ListByGroupAsync(group.Id, status, limit, cursorId, cancellationToken);
        var next = hasMore && items.Count > 0 ? items[^1].Id.ToString() : null;
        return (items.Select(ToResponse).ToList(), new { next_cursor = next, has_more = hasMore });
    }

    /// <summary>REP-03: el administrador del grupo descarta o quita el contenido; los reportes de cuentas no se resuelven aquí.</summary>
    public async Task<ReportResponse> ResolveAsync(ObjectId userId, string id, ResolveReportRequest request, CancellationToken cancellationToken)
    {
        var report = ObjectId.TryParse(id, out var reportId) ? await _reports.GetByIdAsync(reportId, cancellationToken) : null;
        if (report is null)
        {
            throw ApiException.NotFound("El reporte no existe.");
        }

        var (_, member) = await _access.RequireMemberAsync(userId, report.GroupId, cancellationToken);
        RequireAdmin(member.Role);
        if (report.Status != "open")
        {
            throw ApiException.Conflict(ErrorCodes.InvalidReportState, report.Status == "escalated"
                ? "Los reportes de cuentas los revisa el administrador de la plataforma."
                : "El reporte ya se resolvió.");
        }

        var now = DateTime.UtcNow;
        var status = request.Action == "remove_content" ? "content_removed" : "dismissed";
        if (status == "content_removed")
        {
            await RemoveContentAsync(userId, report.GroupId, report.TargetType, report.TargetId, cancellationToken);
            await _reports.ResolveSameTargetAsync(report.GroupId, report.TargetType, report.TargetId, status, userId, now, cancellationToken);
        }

        var resolved = await _reports.TryResolveAsync(report.Id, status, userId, now, cancellationToken)
            ?? await _reports.GetByIdAsync(report.Id, cancellationToken)
            ?? throw ApiException.NotFound("El reporte no existe.");
        await _audit.AppendAsync("report.resolved", userId, "report", report.Id, report.GroupId, new { status }, cancellationToken);
        return ToResponse(resolved);
    }

    /// <summary>REP-04: el administrador quita una foto o un comentario ofensivo del grupo.</summary>
    public async Task RemoveContentAsync(ObjectId userId, string gid, string type, string id, CancellationToken cancellationToken)
    {
        var (group, member) = await _access.RequireMemberAsync(userId, gid, cancellationToken);
        RequireAdmin(member.Role);
        if (type is not ("evidence" or "comment"))
        {
            throw ApiException.Unprocessable(ErrorCodes.ValidationError, "type debe ser evidence o comment.");
        }

        if (!ObjectId.TryParse(id, out var targetId) || !await TargetBelongsAsync(group.Id, type, targetId, cancellationToken))
        {
            throw ApiException.NotFound("No existe ese contenido en el grupo.");
        }

        await RemoveContentAsync(userId, group.Id, type, targetId, cancellationToken);
        await _reports.ResolveSameTargetAsync(group.Id, type, targetId, "content_removed", userId, DateTime.UtcNow, cancellationToken);
    }

    /// <summary>REP-05: bloquear. El bloqueado ya no puede retar directamente a quien lo bloqueó (REP-D2).</summary>
    public async Task BlockAsync(ObjectId userId, string targetUserId, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(targetUserId, out var target) || await _users.GetByIdAsync(target, cancellationToken) is not { DeletedAt: null })
        {
            throw ApiException.NotFound("El usuario no existe.");
        }

        if (target == userId)
        {
            throw ApiException.Unprocessable(ErrorCodes.ValidationError, "No puedes bloquearte a ti mismo.");
        }

        if (await _blocks.TryBlockAsync(new UserBlock { Id = ObjectId.GenerateNewId(), BlockerId = userId, BlockedId = target, CreatedAt = DateTime.UtcNow }, cancellationToken))
        {
            await _audit.AppendAsync("user.blocked", userId, "user", target, null, new { blocked = target.ToString() }, cancellationToken);
        }
    }

    /// <summary>REP-06: quitar el bloqueo (idempotente).</summary>
    public async Task UnblockAsync(ObjectId userId, string targetUserId, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(targetUserId, out var target))
        {
            throw ApiException.NotFound("El usuario no existe.");
        }

        if (await _blocks.UnblockAsync(userId, target, cancellationToken))
        {
            await _audit.AppendAsync("user.unblocked", userId, "user", target, null, new { blocked = target.ToString() }, cancellationToken);
        }
    }

    private async Task RemoveContentAsync(ObjectId actor, ObjectId groupId, string type, ObjectId targetId, CancellationToken cancellationToken)
    {
        if (type == "evidence")
        {
            var removed = await _evidence.MarkRemovedAsync(targetId, DateTime.UtcNow, cancellationToken);
            if (removed?.ObjectKey is string key)
            {
                await _storage.DeleteAsync(key, cancellationToken);
            }
        }
        else if (type == "comment")
        {
            await _votes.ClearCommentAsync(targetId, cancellationToken);
        }

        await _cache.BumpVersionAsync(CacheExtensions.GroupScope(groupId), cancellationToken);
        await _audit.AppendAsync("content.removed", actor, type, targetId, groupId, new { type }, cancellationToken);
    }

    private async Task<bool> TargetBelongsAsync(ObjectId groupId, string type, ObjectId targetId, CancellationToken cancellationToken)
    {
        switch (type)
        {
            case "evidence":
                var evidence = await _evidence.GetByIdAsync(targetId, cancellationToken);
                return evidence is not null && (await _contracts.GetByIdAsync(evidence.ContractId, cancellationToken))?.GroupId == groupId;
            case "comment":
                var vote = await _votes.GetByIdAsync(targetId, cancellationToken);
                return vote is not null && vote.GroupId == groupId && !string.IsNullOrEmpty(vote.Comment);
            case "user":
                try
                {
                    await _access.RequireMemberAsync(targetId, groupId, cancellationToken);
                    return true;
                }
                catch (ApiException)
                {
                    return false;
                }
            default:
                return false;
        }
    }

    private static void RequireAdmin(string role)
    {
        if (role != "admin")
        {
            throw ApiException.Forbidden(ErrorCodes.NotGroupAdmin, "Solo un administrador del grupo puede revisar reportes.");
        }
    }

    private static ReportResponse ToResponse(Report report) => new()
    {
        Id = report.Id.ToString(),
        GroupId = report.GroupId.ToString(),
        ReporterId = report.ReporterId.ToString(),
        TargetType = report.TargetType,
        TargetId = report.TargetId.ToString(),
        Reason = report.Reason,
        Details = report.Details,
        Status = report.Status,
        CreatedAt = report.CreatedAt,
        ResolvedAt = report.ResolvedAt
    };
}

public static class ReportEndpoints
{
    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/reports", async (CreateReportRequest request, ReportService service, HttpContext context, CancellationToken cancellationToken) =>
                ApiResults.Created(await service.CreateAsync(context.User.GetRequiredObjectId(), request, cancellationToken)))
            .WithTags("Reports").RequireAuthorization()
            .AddEndpointFilter<ValidationFilter<CreateReportRequest>>()
            .WithName("CreateReport").Produces<ReportResponse>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status409Conflict);

        app.MapGet("/api/v1/groups/{gid}/reports", async (string gid, string? status, int? limit, string? cursor, ReportService service, HttpContext context, CancellationToken cancellationToken) =>
            {
                var (items, meta) = await service.ListAsync(context.User.GetRequiredObjectId(), gid, status, limit ?? 20, cursor, cancellationToken);
                return ApiResults.Ok(items, meta);
            })
            .WithTags("Reports").RequireAuthorization().WithName("ListGroupReports").Produces<IReadOnlyList<ReportResponse>>().ProducesProblem(StatusCodes.Status403Forbidden);

        app.MapPost("/api/v1/reports/{id}/resolve", async (string id, ResolveReportRequest request, ReportService service, HttpContext context, CancellationToken cancellationToken) =>
                ApiResults.Ok(await service.ResolveAsync(context.User.GetRequiredObjectId(), id, request, cancellationToken)))
            .WithTags("Reports").RequireAuthorization()
            .AddEndpointFilter<ValidationFilter<ResolveReportRequest>>()
            .WithName("ResolveReport").Produces<ReportResponse>().ProducesProblem(StatusCodes.Status403Forbidden).ProducesProblem(StatusCodes.Status409Conflict);

        app.MapDelete("/api/v1/groups/{gid}/content/{type}/{id}", async (string gid, string type, string id, ReportService service, HttpContext context, CancellationToken cancellationToken) =>
            {
                await service.RemoveContentAsync(context.User.GetRequiredObjectId(), gid, type, id, cancellationToken);
                return Results.NoContent();
            })
            .WithTags("Reports").RequireAuthorization().WithName("RemoveGroupContent").Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status403Forbidden);

        app.MapPost("/api/v1/users/{id}/block", async (string id, ReportService service, HttpContext context, CancellationToken cancellationToken) =>
            {
                await service.BlockAsync(context.User.GetRequiredObjectId(), id, cancellationToken);
                return Results.NoContent();
            })
            .WithTags("Reports").RequireAuthorization().WithName("BlockUser").Produces(StatusCodes.Status204NoContent);

        app.MapDelete("/api/v1/users/{id}/block", async (string id, ReportService service, HttpContext context, CancellationToken cancellationToken) =>
            {
                await service.UnblockAsync(context.User.GetRequiredObjectId(), id, cancellationToken);
                return Results.NoContent();
            })
            .WithTags("Reports").RequireAuthorization().WithName("UnblockUser").Produces(StatusCodes.Status204NoContent);

        return app;
    }
}
