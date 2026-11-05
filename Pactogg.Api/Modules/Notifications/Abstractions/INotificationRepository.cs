using MongoDB.Bson;
using Pactogg.Api.Modules.Notifications.Domain;

namespace Pactogg.Api.Modules.Notifications.Abstractions;

public interface INotificationRepository
{
    Task CreateManyAsync(IReadOnlyCollection<Notification> notifications, CancellationToken cancellationToken);
    Task<(IReadOnlyList<Notification> Items, bool HasMore)> ListAsync(
        ObjectId userId,
        bool unreadOnly,
        int limit,
        ObjectId? cursor,
        CancellationToken cancellationToken);
    Task<long> CountUnreadAsync(ObjectId userId, CancellationToken cancellationToken);

    /// <summary>Marca como leído solo si pertenece al usuario. Null si no existe o es de otro.</summary>
    Task<Notification?> MarkReadAsync(ObjectId id, ObjectId userId, DateTime at, CancellationToken cancellationToken);
    Task<long> MarkAllReadAsync(ObjectId userId, DateTime at, CancellationToken cancellationToken);
}
