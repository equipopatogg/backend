using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using Pactogg.Api.Infrastructure.Mongo;

namespace Pactogg.Api.Infrastructure.Audit;

public sealed class MongoAuditService : IAuditService
{
    private readonly MongoContext _context;

    public MongoAuditService(MongoContext context)
    {
        _context = context;
    }

    public async Task AppendAsync(
        string action,
        ObjectId? actorId,
        string entityType,
        ObjectId? entityId,
        ObjectId? groupId,
        object? payload,
        CancellationToken cancellationToken)
    {
        var previous = await _context.AuditEvents.Find(FilterDefinition<AuditEvent>.Empty)
            .SortByDescending(x => x.Id)
            .Limit(1)
            .FirstOrDefaultAsync(cancellationToken);

        var createdAt = DateTime.UtcNow;
        var payloadJson = payload is null ? "{}" : JsonSerializer.Serialize(payload);
        var prevHash = previous?.Hash ?? string.Empty;
        var canonical = $"{prevHash}|{action}|{actorId}|{entityType}|{entityId}|{groupId}|{payloadJson}|{createdAt:O}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();

        await _context.AuditEvents.InsertOneAsync(new AuditEvent
        {
            Id = ObjectId.GenerateNewId(),
            Action = action,
            ActorId = actorId,
            EntityType = entityType,
            EntityId = entityId,
            GroupId = groupId,
            Payload = payloadJson,
            PrevHash = prevHash,
            Hash = hash,
            CreatedAt = createdAt
        }, cancellationToken: cancellationToken);
    }
}
