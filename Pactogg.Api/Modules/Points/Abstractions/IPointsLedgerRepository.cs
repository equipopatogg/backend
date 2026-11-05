using MongoDB.Bson;
using Pactogg.Api.Modules.Points.Domain;

namespace Pactogg.Api.Modules.Points.Abstractions;

/// <summary>Solo lectura: los movimientos se insertan dentro de las transacciones de contratos.</summary>
public interface IPointsLedgerRepository
{
    Task<(IReadOnlyList<PointsLedgerEntry> Items, bool HasMore)> ListAsync(
        ObjectId groupId,
        ObjectId userId,
        string? type,
        int limit,
        ObjectId? cursor,
        CancellationToken cancellationToken);
}
