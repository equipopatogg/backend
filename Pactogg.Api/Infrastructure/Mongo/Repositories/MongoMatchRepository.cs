using MongoDB.Bson;
using MongoDB.Driver;
using Pactogg.Api.Modules.Tournaments.Abstractions;
using Pactogg.Api.Modules.Tournaments.Domain;

namespace Pactogg.Api.Infrastructure.Mongo.Repositories;

public sealed class MongoMatchRepository : IMatchRepository
{
    private readonly MongoContext _context;

    public MongoMatchRepository(MongoContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Match>> ListForTournamentIdsAsync(
        IReadOnlyCollection<ObjectId> tournamentIds,
        CancellationToken cancellationToken)
    {
        if (tournamentIds.Count == 0)
        {
            return [];
        }

        return await _context.Matches.Find(x => tournamentIds.Contains(x.TournamentId))
            .SortBy(x => x.Round)
            .ThenBy(x => x.Position)
            .ToListAsync(cancellationToken);
    }

    public async Task<Match?> GetByIdAsync(ObjectId id, CancellationToken cancellationToken) =>
        await _context.Matches.Find(x => x.Id == id).FirstOrDefaultAsync(cancellationToken);

    public async Task<Match?> FindNextForGroupsAsync(IReadOnlyCollection<ObjectId> groupIds, CancellationToken cancellationToken)
    {
        if (groupIds.Count == 0)
        {
            return null;
        }

        var builder = Builders<Match>.Filter;
        var filter = builder.Eq(x => x.Status, "pending")
            & builder.Ne(x => x.P1GroupId, null)
            & builder.Ne(x => x.P2GroupId, null)
            & (builder.In(x => x.P1GroupId, groupIds.Select(x => (ObjectId?)x)) | builder.In(x => x.P2GroupId, groupIds.Select(x => (ObjectId?)x)));
        return await _context.Matches.Find(filter).SortBy(x => x.Round).ThenBy(x => x.Id).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Match?> TryReportAsync(ObjectId id, int p1Score, int p2Score, ObjectId winnerGroupId, ObjectId reporterId, ObjectId reporterGroupId, bool reporterIsP1, string screenshotKey, DateTime now, CancellationToken cancellationToken)
    {
        var update = Builders<Match>.Update
            .Set(x => x.Status, "reported")
            .Set(x => x.P1Score, p1Score)
            .Set(x => x.P2Score, p2Score)
            .Set(x => x.WinnerGroupId, winnerGroupId)
            .Set(x => x.ReportedBy, reporterId)
            .Set(x => x.ReportedByGroupId, reporterGroupId)
            .Set(x => x.ReportedAt, now)
            .Set(x => x.ScreenshotKey, screenshotKey)
            .Set(x => x.UpdatedAt, now);
        update = reporterIsP1 ? update.AddToSet(x => x.P1Representatives, reporterId) : update.AddToSet(x => x.P2Representatives, reporterId);
        return await _context.Matches.FindOneAndUpdateAsync(
            x => x.Id == id && x.Status == "pending" && x.P1GroupId != null && x.P2GroupId != null,
            update,
            new FindOneAndUpdateOptions<Match> { ReturnDocument = ReturnDocument.After },
            cancellationToken);
    }

    public async Task<Match?> TryDisputeAsync(ObjectId id, ObjectId userId, bool userIsP1, string reason, DateTime now, CancellationToken cancellationToken)
    {
        var update = Builders<Match>.Update
            .Set(x => x.Status, "disputed")
            .Set(x => x.DisputeReason, reason)
            .Set(x => x.DisputedBy, userId)
            .Set(x => x.DisputedAt, now)
            .Set(x => x.UpdatedAt, now);
        update = userIsP1 ? update.AddToSet(x => x.P1Representatives, userId) : update.AddToSet(x => x.P2Representatives, userId);
        return await _context.Matches.FindOneAndUpdateAsync(
            x => x.Id == id && x.Status == "reported",
            update,
            new FindOneAndUpdateOptions<Match> { ReturnDocument = ReturnDocument.After },
            cancellationToken);
    }

    public async Task<Match?> TryConfirmAsync(ObjectId id, IReadOnlyCollection<string> fromStatuses, ObjectId winnerGroupId, bool clearScores, ObjectId? confirmerId, bool confirmerIsP1, ObjectId? resolvedBy, string resolution, DateTime now, CancellationToken cancellationToken)
    {
        var update = Builders<Match>.Update
            .Set(x => x.Status, "confirmed")
            .Set(x => x.WinnerGroupId, winnerGroupId)
            .Set(x => x.ConfirmedAt, now)
            .Set(x => x.Resolution, resolution)
            .Set(x => x.ResolvedBy, resolvedBy)
            .Set(x => x.UpdatedAt, now);
        if (clearScores)
        {
            update = update.Set(x => x.P1Score, null).Set(x => x.P2Score, null);
        }

        if (confirmerId is ObjectId confirmer)
        {
            update = confirmerIsP1 ? update.AddToSet(x => x.P1Representatives, confirmer) : update.AddToSet(x => x.P2Representatives, confirmer);
        }

        return await _context.Matches.FindOneAndUpdateAsync(
            Builders<Match>.Filter.Eq(x => x.Id, id) & Builders<Match>.Filter.In(x => x.Status, fromStatuses),
            update,
            new FindOneAndUpdateOptions<Match> { ReturnDocument = ReturnDocument.After },
            cancellationToken);
    }

    public Task PlaceWinnerAsync(ObjectId nextMatchId, bool asP1, ObjectId groupId, DateTime now, CancellationToken cancellationToken) =>
        _context.Matches.UpdateOneAsync(
            x => x.Id == nextMatchId,
            (asP1 ? Builders<Match>.Update.Set(x => x.P1GroupId, groupId) : Builders<Match>.Update.Set(x => x.P2GroupId, groupId))
                .Set(x => x.UpdatedAt, now),
            cancellationToken: cancellationToken);
}
