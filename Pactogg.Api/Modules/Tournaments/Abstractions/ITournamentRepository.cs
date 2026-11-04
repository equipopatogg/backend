using MongoDB.Bson;
using Pactogg.Api.Modules.Tournaments.Domain;

namespace Pactogg.Api.Modules.Tournaments.Abstractions;

public interface ITournamentRepository
{
    Task<(IReadOnlyList<Tournament> Items, bool HasMore)> ListForGroupsAsync(
        IReadOnlyCollection<ObjectId> groupIds,
        string? status,
        string? game,
        ObjectId? groupId,
        int limit,
        ObjectId? cursor,
        CancellationToken cancellationToken);
}
