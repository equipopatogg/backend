using MongoDB.Bson;
using MongoDB.Driver;
using Pactogg.Api.Modules.Notifications.Abstractions;
using Pactogg.Api.Modules.Notifications.Domain;

namespace Pactogg.Api.Infrastructure.Mongo.Repositories;

public sealed class MongoNotificationRepository : INotificationRepository
{
    private readonly MongoContext _context;

    public MongoNotificationRepository(MongoContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Notification>> CreateManyAsync(IReadOnlyList<Notification> notifications, CancellationToken cancellationToken)
    {
        if (notifications.Count == 0)
        {
            return [];
        }

        try
        {
            // Sin orden: si un aviso ya existía (dedupe_key duplicada) los demás sí se insertan.
            await _context.Notifications.InsertManyAsync(notifications, new InsertManyOptions { IsOrdered = false }, cancellationToken);
            return notifications;
        }
        catch (MongoBulkWriteException ex) when (ex.WriteErrors.All(x => x.Category == ServerErrorCategory.DuplicateKey))
        {
            // Evento repetido (por ejemplo, dos instancias procesando el mismo vencimiento): se omite.
            var failed = ex.WriteErrors.Select(x => x.Index).ToHashSet();
            return notifications.Where((_, index) => !failed.Contains(index)).ToList();
        }
    }

    public async Task<(IReadOnlyList<Notification> Items, bool HasMore)> ListAsync(
        ObjectId userId,
        bool unreadOnly,
        int limit,
        ObjectId? cursor,
        CancellationToken cancellationToken)
    {
        var builder = Builders<Notification>.Filter;
        var filter = builder.Eq(x => x.UserId, userId);
        if (unreadOnly)
        {
            filter &= builder.Eq(x => x.ReadAt, null);
        }

        if (cursor.HasValue)
        {
            filter &= builder.Lt(x => x.Id, cursor.Value);
        }

        var items = await _context.Notifications.Find(filter).SortByDescending(x => x.Id).Limit(limit + 1).ToListAsync(cancellationToken);
        return (items.Take(limit).ToList(), items.Count > limit);
    }

    public Task<long> CountUnreadAsync(ObjectId userId, CancellationToken cancellationToken) =>
        _context.Notifications.CountDocumentsAsync(x => x.UserId == userId && x.ReadAt == null, cancellationToken: cancellationToken);

    public async Task<bool> DeleteAsync(ObjectId id, ObjectId userId, CancellationToken cancellationToken) =>
        (await _context.Notifications.DeleteOneAsync(x => x.Id == id && x.UserId == userId, cancellationToken)).DeletedCount > 0;

    public async Task<Notification?> MarkReadAsync(ObjectId id, ObjectId userId, DateTime at, CancellationToken cancellationToken)
    {
        var notification = await _context.Notifications.Find(x => x.Id == id && x.UserId == userId).FirstOrDefaultAsync(cancellationToken);
        if (notification is null || notification.ReadAt is not null)
        {
            return notification;
        }

        return await _context.Notifications.FindOneAndUpdateAsync(
            Builders<Notification>.Filter.Where(x => x.Id == id && x.UserId == userId),
            Builders<Notification>.Update.Set(x => x.ReadAt, at),
            new FindOneAndUpdateOptions<Notification> { ReturnDocument = ReturnDocument.After },
            cancellationToken);
    }

    public async Task<long> MarkAllReadAsync(ObjectId userId, DateTime at, CancellationToken cancellationToken)
    {
        var result = await _context.Notifications.UpdateManyAsync(
            x => x.UserId == userId && x.ReadAt == null,
            Builders<Notification>.Update.Set(x => x.ReadAt, at),
            cancellationToken: cancellationToken);
        return result.ModifiedCount;
    }
}
