using MongoDB.Bson;
using Pactogg.Api.Common;
using Pactogg.Api.Infrastructure.Push;
using Pactogg.Api.Modules.Notifications.Abstractions;
using Pactogg.Api.Modules.Notifications.Domain;
using Pactogg.Api.Modules.Notifications.Dtos;

namespace Pactogg.Api.Modules.Notifications;

/// <summary>NOT-01..05 (NOT-05 interno: guarda el aviso y encola el push). Los avisos son "mejor esfuerzo": si fallan no rompen la acción principal.</summary>
public sealed class NotificationService
{
    private readonly INotificationRepository _notifications;
    private readonly ILogger<NotificationService> _logger;
    private readonly PushQueue _push;

    public NotificationService(INotificationRepository notifications, ILogger<NotificationService> logger, PushQueue push)
    {
        _notifications = notifications;
        _logger = logger;
        _push = push;
    }

    public async Task NotifyAsync(
        IEnumerable<ObjectId> userIds,
        string type,
        string title,
        string body,
        ObjectId? groupId = null,
        ObjectId? contractId = null,
        ObjectId? commitmentId = null,
        CancellationToken cancellationToken = default,
        string? dedupeSuffix = null)
    {
        var now = DateTime.UtcNow;
        var reference = contractId ?? commitmentId ?? groupId;
        var items = userIds.Distinct().Select(userId => new Notification
        {
            Id = ObjectId.GenerateNewId(),
            UserId = userId,
            Type = type,
            Title = Truncate(title, 80),
            Body = Truncate(body, 200),
            GroupId = groupId,
            ContractId = contractId,
            CommitmentId = commitmentId,
            DedupeKey = reference is null ? null : $"{type}:{reference}:{userId}{(dedupeSuffix is null ? string.Empty : ":" + dedupeSuffix)}",
            CreatedAt = now
        }).ToList();

        IReadOnlyList<Notification> inserted;
        try
        {
            inserted = await _notifications.CreateManyAsync(items, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "No se pudieron guardar {Count} avisos de tipo {Type}.", items.Count, type);
            return;
        }

        if (inserted.Count == 0)
        {
            return;
        }

        // NOT-05: además del centro de notificaciones, push por FCM (en segundo plano).
        var data = new Dictionary<string, string> { ["type"] = type };
        if (groupId is ObjectId g)
        {
            data["group_id"] = g.ToString();
        }

        if (contractId is ObjectId c)
        {
            data["contract_id"] = c.ToString();
        }

        if (commitmentId is ObjectId m)
        {
            data["commitment_id"] = m.ToString();
        }

        _push.TryEnqueue(new PushMessage(inserted.Select(x => x.UserId).ToArray(), type, Truncate(title, 80), Truncate(body, 200), data));
    }

    public async Task<(IReadOnlyList<NotificationResponse> Items, object Meta)> ListAsync(
        ObjectId userId,
        bool unreadOnly,
        int limit,
        string? cursor,
        CancellationToken cancellationToken)
    {
        ListQuery.ValidateLimit(limit);
        var cursorId = ListQuery.ParseCursor(cursor);
        var (items, hasMore) = await _notifications.ListAsync(userId, unreadOnly, limit, cursorId, cancellationToken);
        var unread = await _notifications.CountUnreadAsync(userId, cancellationToken);
        var next = hasMore && items.Count > 0 ? items[^1].Id.ToString() : null;
        return (items.Select(ToResponse).ToList(), new { next_cursor = next, has_more = hasMore, unread_count = unread });
    }

    public async Task<NotificationResponse> MarkReadAsync(ObjectId userId, string id, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(id, out var notificationId))
        {
            throw ApiException.NotFound("El aviso no existe.");
        }

        var notification = await _notifications.MarkReadAsync(notificationId, userId, DateTime.UtcNow, cancellationToken)
            ?? throw ApiException.NotFound("El aviso no existe.");
        return ToResponse(notification);
    }

    /// <summary>NOT-04: el dueño elimina un aviso.</summary>
    public async Task DeleteAsync(ObjectId userId, string id, CancellationToken cancellationToken)
    {
        if (!ObjectId.TryParse(id, out var notificationId) || !await _notifications.DeleteAsync(notificationId, userId, cancellationToken))
        {
            throw ApiException.NotFound("El aviso no existe.");
        }
    }

    public async Task<ReadAllResponse> MarkAllReadAsync(ObjectId userId, CancellationToken cancellationToken) =>
        new() { Updated = await _notifications.MarkAllReadAsync(userId, DateTime.UtcNow, cancellationToken) };

    private static NotificationResponse ToResponse(Notification notification) => new()
    {
        Id = notification.Id.ToString(),
        Type = notification.Type,
        Title = notification.Title,
        Body = notification.Body,
        GroupId = notification.GroupId?.ToString(),
        ContractId = notification.ContractId?.ToString(),
        CommitmentId = notification.CommitmentId?.ToString(),
        Read = notification.ReadAt is not null,
        ReadAt = notification.ReadAt,
        CreatedAt = notification.CreatedAt
    };

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..(max - 1)] + "…";
}
