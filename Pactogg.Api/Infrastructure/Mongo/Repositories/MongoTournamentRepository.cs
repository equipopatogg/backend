using MongoDB.Bson;
using MongoDB.Driver;
using Pactogg.Api.Modules.Groups.Domain;
using Pactogg.Api.Modules.Points.Domain;
using Pactogg.Api.Modules.Tournaments.Abstractions;
using Pactogg.Api.Modules.Tournaments.Domain;

namespace Pactogg.Api.Infrastructure.Mongo.Repositories;

public sealed class MongoTournamentRepository : ITournamentRepository
{
    private static readonly string[] ActiveStatuses = ["open", "in_progress"];
    private static readonly string[] ClosedMatchStatuses = ["confirmed", "bye"];

    private readonly MongoContext _context;

    public MongoTournamentRepository(MongoContext context)
    {
        _context = context;
    }

    public async Task<(IReadOnlyList<Tournament> Items, bool HasMore)> ListForGroupsAsync(
        IReadOnlyCollection<ObjectId> groupIds,
        string? status,
        string? game,
        ObjectId? groupId,
        int limit,
        ObjectId? cursor,
        CancellationToken cancellationToken)
    {
        if (groupIds.Count == 0)
        {
            return ([], false);
        }

        var builder = Builders<Tournament>.Filter;
        var filter = builder.In(x => x.GroupId, groupIds) | builder.AnyIn(x => x.Participants, groupIds);
        if (!string.IsNullOrWhiteSpace(status))
        {
            filter &= builder.Eq(x => x.Status, status);
        }

        if (!string.IsNullOrWhiteSpace(game))
        {
            filter &= builder.Eq(x => x.Game, game);
        }

        if (groupId.HasValue)
        {
            filter &= builder.Eq(x => x.GroupId, groupId.Value) | builder.AnyEq(x => x.Participants, groupId.Value);
        }

        if (cursor.HasValue)
        {
            filter &= builder.Lt(x => x.Id, cursor.Value);
        }

        var items = await _context.Tournaments.Find(filter)
            .SortByDescending(x => x.Id)
            .Limit(limit + 1)
            .ToListAsync(cancellationToken);

        var hasMore = items.Count > limit;
        return (items.Take(limit).ToList(), hasMore);
    }

    public async Task<Tournament?> GetByIdAsync(ObjectId id, CancellationToken cancellationToken) =>
        await _context.Tournaments.Find(x => x.Id == id).FirstOrDefaultAsync(cancellationToken);

    public async Task<Tournament?> GetByCodeAsync(string code, CancellationToken cancellationToken) =>
        await _context.Tournaments.Find(x => x.Code == code).FirstOrDefaultAsync(cancellationToken);

    public async Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken) =>
        await _context.Tournaments.Find(x => x.Code == code).AnyAsync(cancellationToken);

    public async Task<bool> HasActiveForOrganizerGroupAsync(ObjectId groupId, CancellationToken cancellationToken) =>
        await _context.Tournaments.Find(Builders<Tournament>.Filter.Eq(x => x.GroupId, groupId) & Builders<Tournament>.Filter.In(x => x.Status, ActiveStatuses))
            .AnyAsync(cancellationToken);

    public Task<TournamentWriteResult> CreateAsync(Tournament tournament, DateTime now, CancellationToken cancellationToken) =>
        InTransactionAsync(async session =>
        {
            await _context.Tournaments.InsertOneAsync(session, tournament, cancellationToken: cancellationToken);
            if (tournament.EntryPoints > 0 && !await DebitAsync(session, tournament.GroupId, tournament.OrganizerId, tournament.EntryPoints, tournament.Id, now, cancellationToken))
            {
                return new TournamentWriteResult(TournamentWriteOutcome.InsufficientPoints, null);
            }

            return new TournamentWriteResult(TournamentWriteOutcome.Ok, tournament);
        }, cancellationToken);

    public Task<TournamentWriteResult> JoinAsync(ObjectId id, int expectedMaxGroups, int amount, ObjectId groupId, ObjectId payerId, DateTime now, CancellationToken cancellationToken) =>
        InTransactionAsync(async session =>
        {
            var builder = Builders<Tournament>.Filter;
            var filter = builder.Eq(x => x.Id, id)
                & builder.Eq(x => x.Status, "open")
                & builder.Eq(x => x.MaxGroups, expectedMaxGroups)
                & builder.Eq(x => x.EntryPoints, amount)
                & builder.Not(builder.AnyEq(x => x.Participants, groupId))
                & builder.Exists($"participants.{expectedMaxGroups - 1}", false);
            var updated = await _context.Tournaments.FindOneAndUpdateAsync(
                session,
                filter,
                Builders<Tournament>.Update
                    .Push(x => x.Participants, groupId)
                    .Push(x => x.Entries, new TournamentEntry { GroupId = groupId, PaidBy = payerId, Amount = amount, Status = "active", JoinedAt = now })
                    .Inc(x => x.PrizePool, amount)
                    .Set(x => x.UpdatedAt, now),
                new FindOneAndUpdateOptions<Tournament> { ReturnDocument = ReturnDocument.After },
                cancellationToken);
            if (updated is null)
            {
                return new TournamentWriteResult(TournamentWriteOutcome.StateChanged, null);
            }

            if (amount > 0 && !await DebitAsync(session, groupId, payerId, amount, id, now, cancellationToken))
            {
                return new TournamentWriteResult(TournamentWriteOutcome.InsufficientPoints, null);
            }

            return new TournamentWriteResult(TournamentWriteOutcome.Ok, updated);
        }, cancellationToken);

    public async Task<Tournament?> LeaveAsync(ObjectId id, ObjectId groupId, DateTime now, CancellationToken cancellationToken)
    {
        var result = await InTransactionAsync(async session =>
        {
            var builder = Builders<Tournament>.Filter;
            var filter = builder.Eq(x => x.Id, id)
                & builder.Eq(x => x.Status, "open")
                & builder.Ne(x => x.GroupId, groupId)
                & builder.AnyEq(x => x.Participants, groupId)
                & builder.ElemMatch(x => x.Entries, e => e.GroupId == groupId && e.Status == "active");
            var before = await _context.Tournaments.FindOneAndUpdateAsync(
                session,
                filter,
                Builders<Tournament>.Update
                    .Pull(x => x.Participants, groupId)
                    .Set("entries.$[e].status", "refunded")
                    .Set(x => x.UpdatedAt, now),
                new FindOneAndUpdateOptions<Tournament>
                {
                    ReturnDocument = ReturnDocument.Before,
                    ArrayFilters = [new BsonDocumentArrayFilterDefinition<BsonDocument>(new BsonDocument { { "e.group_id", groupId }, { "e.status", "active" } })]
                },
                cancellationToken);
            if (before is null)
            {
                return new TournamentWriteResult(TournamentWriteOutcome.StateChanged, null);
            }

            var entry = before.Entries.First(e => e.GroupId == groupId && e.Status == "active");
            if (entry.Amount > 0)
            {
                await _context.Tournaments.UpdateOneAsync(session, x => x.Id == id, Builders<Tournament>.Update.Inc(x => x.PrizePool, -entry.Amount), cancellationToken: cancellationToken);
                await CreditAsync(session, entry.GroupId, entry.PaidBy, entry.Amount, id, now, cancellationToken);
            }

            return new TournamentWriteResult(TournamentWriteOutcome.Ok, await _context.Tournaments.Find(session, x => x.Id == id).FirstAsync(cancellationToken));
        }, cancellationToken);
        return result.Tournament;
    }

    public async Task<Tournament?> UpdateAsync(ObjectId id, string game, DateTime scheduledAt, int maxGroups, DateTime now, CancellationToken cancellationToken)
    {
        var builder = Builders<Tournament>.Filter;
        var filter = builder.Eq(x => x.Id, id)
            & builder.Eq(x => x.Status, "open")
            & builder.Exists($"participants.{maxGroups}", false);
        return await _context.Tournaments.FindOneAndUpdateAsync(
            filter,
            Builders<Tournament>.Update
                .Set(x => x.Game, game)
                .Set(x => x.ScheduledAt, scheduledAt)
                .Set(x => x.MaxGroups, maxGroups)
                .Set(x => x.UpdatedAt, now),
            new FindOneAndUpdateOptions<Tournament> { ReturnDocument = ReturnDocument.After },
            cancellationToken);
    }

    public async Task<Tournament?> CancelAsync(ObjectId id, string reason, DateTime now, CancellationToken cancellationToken)
    {
        var result = await InTransactionAsync(async session =>
        {
            var before = await _context.Tournaments.FindOneAndUpdateAsync(
                session,
                Builders<Tournament>.Filter.Eq(x => x.Id, id) & Builders<Tournament>.Filter.In(x => x.Status, ActiveStatuses),
                Builders<Tournament>.Update
                    .Set(x => x.Status, "cancelled")
                    .Set(x => x.CancelledAt, now)
                    .Set(x => x.CancelReason, reason)
                    .Set(x => x.PrizePool, 0)
                    .Set("entries.$[e].status", "refunded")
                    .Set(x => x.UpdatedAt, now),
                new FindOneAndUpdateOptions<Tournament>
                {
                    ReturnDocument = ReturnDocument.Before,
                    ArrayFilters = [new BsonDocumentArrayFilterDefinition<BsonDocument>(new BsonDocument("e.status", "active"))]
                },
                cancellationToken);
            if (before is null)
            {
                return new TournamentWriteResult(TournamentWriteOutcome.StateChanged, null);
            }

            foreach (var entry in before.Entries.Where(e => e.Status == "active" && e.Amount > 0))
            {
                await CreditAsync(session, entry.GroupId, entry.PaidBy, entry.Amount, id, now, cancellationToken);
            }

            await _context.Matches.UpdateManyAsync(
                session,
                Builders<Match>.Filter.Eq(x => x.TournamentId, id) & Builders<Match>.Filter.Nin(x => x.Status, ClosedMatchStatuses),
                Builders<Match>.Update.Set(x => x.Status, "cancelled").Set(x => x.UpdatedAt, now),
                cancellationToken: cancellationToken);

            return new TournamentWriteResult(TournamentWriteOutcome.Ok, await _context.Tournaments.Find(session, x => x.Id == id).FirstAsync(cancellationToken));
        }, cancellationToken);
        return result.Tournament;
    }

    public async Task<Tournament?> StartAsync(ObjectId id, IReadOnlyList<ObjectId> expectedParticipants, IReadOnlyList<Match> matches, DateTime now, CancellationToken cancellationToken)
    {
        var result = await InTransactionAsync(async session =>
        {
            var builder = Builders<Tournament>.Filter;
            var updated = await _context.Tournaments.FindOneAndUpdateAsync(
                session,
                builder.Eq(x => x.Id, id) & builder.Eq(x => x.Status, "open") & builder.Eq(x => x.Participants, expectedParticipants.ToList()),
                Builders<Tournament>.Update
                    .Set(x => x.Status, "in_progress")
                    .Set(x => x.StartedAt, now)
                    .Set(x => x.UpdatedAt, now),
                new FindOneAndUpdateOptions<Tournament> { ReturnDocument = ReturnDocument.After },
                cancellationToken);
            if (updated is null)
            {
                return new TournamentWriteResult(TournamentWriteOutcome.StateChanged, null);
            }

            await _context.Matches.InsertManyAsync(session, matches, cancellationToken: cancellationToken);
            return new TournamentWriteResult(TournamentWriteOutcome.Ok, updated);
        }, cancellationToken);
        return result.Tournament;
    }

    /// <summary>Resta del saldo del miembro solo si le alcanza; registra tournament_entry.</summary>
    private async Task<bool> DebitAsync(IClientSessionHandle session, ObjectId groupId, ObjectId userId, int amount, ObjectId tournamentId, DateTime now, CancellationToken cancellationToken)
    {
        var member = await _context.GroupMembers.FindOneAndUpdateAsync(
            session,
            Builders<GroupMember>.Filter.Where(x => x.GroupId == groupId && x.UserId == userId && x.Status == "active" && x.PointsBalance >= amount),
            Builders<GroupMember>.Update.Inc(x => x.PointsBalance, -amount).Set(x => x.UpdatedAt, now),
            new FindOneAndUpdateOptions<GroupMember> { ReturnDocument = ReturnDocument.After },
            cancellationToken);
        if (member is null)
        {
            return false;
        }

        await InsertLedgerAsync(session, groupId, userId, -amount, member.PointsBalance, tournamentId, now, cancellationToken);
        return true;
    }

    /// <summary>Movimiento compensatorio: devuelve la inscripción con tournament_entry positivo (F2-D66).</summary>
    private async Task CreditAsync(IClientSessionHandle session, ObjectId groupId, ObjectId userId, int amount, ObjectId tournamentId, DateTime now, CancellationToken cancellationToken)
    {
        var member = await _context.GroupMembers.FindOneAndUpdateAsync(
            session,
            Builders<GroupMember>.Filter.Where(x => x.GroupId == groupId && x.UserId == userId),
            Builders<GroupMember>.Update.Inc(x => x.PointsBalance, amount).Set(x => x.UpdatedAt, now),
            new FindOneAndUpdateOptions<GroupMember> { ReturnDocument = ReturnDocument.After },
            cancellationToken);
        if (member is not null)
        {
            await InsertLedgerAsync(session, groupId, userId, amount, member.PointsBalance, tournamentId, now, cancellationToken);
        }
    }

    private Task InsertLedgerAsync(IClientSessionHandle session, ObjectId groupId, ObjectId userId, int amount, int balanceAfter, ObjectId tournamentId, DateTime now, CancellationToken cancellationToken) =>
        _context.PointsLedger.InsertOneAsync(
            session,
            new PointsLedgerEntry
            {
                Id = ObjectId.GenerateNewId(),
                GroupId = groupId,
                UserId = userId,
                Type = "tournament_entry",
                Amount = amount,
                RefType = "tournament",
                RefId = tournamentId,
                BalanceAfter = balanceAfter,
                CreatedAt = now
            },
            cancellationToken: cancellationToken);

    /// <summary>Todo o nada; un resultado distinto de Ok aborta. Conflicto de escritura → StateChanged.</summary>
    private async Task<TournamentWriteResult> InTransactionAsync(Func<IClientSessionHandle, Task<TournamentWriteResult>> work, CancellationToken cancellationToken)
    {
        using var session = await _context.Client.StartSessionAsync(cancellationToken: cancellationToken);
        session.StartTransaction();
        try
        {
            var result = await work(session);
            if (result.Outcome != TournamentWriteOutcome.Ok)
            {
                await session.AbortTransactionAsync(cancellationToken);
                return result;
            }

            await session.CommitTransactionAsync(cancellationToken);
            return result;
        }
        catch (MongoException ex) when (ex.HasErrorLabel("TransientTransactionError"))
        {
            if (session.IsInTransaction)
            {
                await session.AbortTransactionAsync(CancellationToken.None);
            }

            return new TournamentWriteResult(TournamentWriteOutcome.StateChanged, null);
        }
        catch
        {
            if (session.IsInTransaction)
            {
                await session.AbortTransactionAsync(CancellationToken.None);
            }

            throw;
        }
    }
}
