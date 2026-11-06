using MongoDB.Bson;

namespace Pactogg.Api.Infrastructure.Audit;

/// <summary>AUD-01: lectura del historial. La auditoría nunca se modifica ni se elimina (AUD-02).</summary>
public interface IAuditReader
{
    Task<(IReadOnlyList<AuditEvent> Items, bool HasMore)> ListByGroupAsync(
        ObjectId groupId,
        string? entityType,
        ObjectId? entityId,
        int limit,
        ObjectId? cursor,
        CancellationToken cancellationToken);
}
