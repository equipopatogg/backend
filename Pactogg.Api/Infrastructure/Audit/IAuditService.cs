using MongoDB.Bson;

namespace Pactogg.Api.Infrastructure.Audit;

public interface IAuditService
{
    Task AppendAsync(
        string action,
        ObjectId? actorId,
        string entityType,
        ObjectId? entityId,
        ObjectId? groupId,
        object? payload,
        CancellationToken cancellationToken);
}
