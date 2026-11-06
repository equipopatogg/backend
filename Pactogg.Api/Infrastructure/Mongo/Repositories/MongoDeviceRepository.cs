using MongoDB.Bson;
using MongoDB.Driver;
using Pactogg.Api.Modules.Devices;

namespace Pactogg.Api.Infrastructure.Mongo.Repositories;

public sealed class MongoDeviceRepository : IDeviceRepository
{
    private readonly MongoContext _context;

    public MongoDeviceRepository(MongoContext context)
    {
        _context = context;
    }

    public async Task<Device> UpsertAsync(ObjectId userId, string fcmToken, string platform, string? appVersion, DateTime now, CancellationToken cancellationToken) =>
        await _context.Devices.FindOneAndUpdateAsync(
            Builders<Device>.Filter.Eq(x => x.FcmToken, fcmToken),
            Builders<Device>.Update
                .Set(x => x.UserId, userId)
                .Set(x => x.Platform, platform)
                .Set(x => x.AppVersion, appVersion)
                .Set(x => x.UpdatedAt, now)
                .SetOnInsert(x => x.CreatedAt, now),
            new FindOneAndUpdateOptions<Device> { IsUpsert = true, ReturnDocument = ReturnDocument.After },
            cancellationToken);

    public async Task<long> DeleteAsync(ObjectId userId, string? fcmToken, CancellationToken cancellationToken)
    {
        var filter = Builders<Device>.Filter.Eq(x => x.UserId, userId);
        if (fcmToken is not null)
        {
            filter &= Builders<Device>.Filter.Eq(x => x.FcmToken, fcmToken);
        }

        return (await _context.Devices.DeleteManyAsync(filter, cancellationToken)).DeletedCount;
    }

    public async Task<IReadOnlyList<Device>> ListByUsersAsync(IReadOnlyCollection<ObjectId> userIds, CancellationToken cancellationToken) =>
        userIds.Count == 0
            ? []
            : await _context.Devices.Find(Builders<Device>.Filter.In(x => x.UserId, userIds)).ToListAsync(cancellationToken);

    public Task DeleteTokensAsync(IReadOnlyCollection<string> fcmTokens, CancellationToken cancellationToken) =>
        fcmTokens.Count == 0
            ? Task.CompletedTask
            : _context.Devices.DeleteManyAsync(Builders<Device>.Filter.In(x => x.FcmToken, fcmTokens), cancellationToken);
}
