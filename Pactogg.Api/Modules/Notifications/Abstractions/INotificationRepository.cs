using MongoDB.Bson;
using Pactogg.Api.Modules.Notifications.Domain;

namespace Pactogg.Api.Modules.Notifications.Abstractions;

public interface INotificationRepository
{
    /// <summary>Inserta y devuelve los que sí se guardaron (los duplicados por dedupe_key se omiten).</summary>
    Task<IReadOnlyList<Notification>> CreateManyAsync(IReadOnlyList<Notification> notifications, CancellationToken cancellationToken);
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
    Task<bool> DeleteAsync(ObjectId id, ObjectId userId, CancellationToken cancellationToken);
}
