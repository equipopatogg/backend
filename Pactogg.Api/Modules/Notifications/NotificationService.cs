using MongoDB.Bson;
using Pactogg.Api.Common;
using Pactogg.Api.Modules.Notifications.Abstractions;
using Pactogg.Api.Modules.Notifications.Domain;
using Pactogg.Api.Modules.Notifications.Dtos;

namespace Pactogg.Api.Modules.Notifications;

/// <summary>NOT-01..03 y NOT-05 (interno). Los avisos son "mejor esfuerzo": si fallan no rompen la acción principal.</summary>
public sealed class NotificationService
{
    private readonly INotificationRepository _notifications;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(INotificationRepository notifications, ILogger<NotificationService> logger)
    {
        _notifications = notifications;
        _logger = logger;
    }

    public async Task NotifyAsync(
        IEnumerable<ObjectId> userIds,
        string type,
        string title,
        string body,
        ObjectId? groupId = null,
        ObjectId? contractId = null,
        ObjectId? commitmentId = null,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
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
            CreatedAt = now
        }).ToList();

        try
        {
            await _notifications.CreateManyAsync(items, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "No se pudieron guardar {Count} avisos de tipo {Type}.", items.Count, type);
        }
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
