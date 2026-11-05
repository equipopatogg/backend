using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;
using Pactogg.Api.Modules.Contracts.Abstractions;
using Pactogg.Api.Modules.Contracts.Domain;
using Pactogg.Api.Modules.Auth.Domain;
using Pactogg.Api.Modules.Commitments.Domain;
using Pactogg.Api.Modules.Groups.Domain;
using Pactogg.Api.Modules.Points.Domain;

namespace Pactogg.Api.Infrastructure.Mongo.Repositories;

public sealed class MongoContractRepository : IContractRepository
{
    private readonly MongoContext _context;

    public MongoContractRepository(MongoContext context)
    {
        _context = context;
    }

    public Task CreateAsync(Contract contract, CancellationToken cancellationToken) =>
        _context.Contracts.InsertOneAsync(contract, cancellationToken: cancellationToken);

    public async Task<Contract?> GetByIdAsync(ObjectId id, CancellationToken cancellationToken) =>
        await _context.Contracts.Find(x => x.Id == id).FirstOrDefaultAsync(cancellationToken);

    public async Task<(IReadOnlyList<Contract> Items, bool HasMore)> ListForGroupAsync(
        ObjectId groupId,
        ContractListFilter filter,
        int limit,
        ObjectId? cursor,
        CancellationToken cancellationToken)
    {
        var builder = Builders<Contract>.Filter;
        var query = builder.Eq(x => x.GroupId, groupId);
        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            query &= builder.Eq(x => x.Status, filter.Status);
        }

        if (filter.Mine.HasValue)
        {
            var me = filter.Mine.Value;
            query &= builder.Or(
                builder.Eq(x => x.ChallengerId, me),
                builder.Eq(x => x.TargetId, me),
                builder.Eq(x => x.AcceptedBy, me));
        }

        if (!string.IsNullOrWhiteSpace(filter.Game))
        {
            query &= builder.Regex(x => x.Game, new BsonRegularExpression("^" + Regex.Escape(filter.Game) + "$", "i"));
        }

        if (!string.IsNullOrWhiteSpace(filter.Query))
        {
            var pattern = new BsonRegularExpression(Regex.Escape(filter.Query), "i");
            query &= builder.Or(
                builder.Regex(x => x.Condition, pattern),
                builder.Regex(x => x.Reward, pattern),
                builder.Regex(x => x.Game, pattern));
        }

        if (cursor.HasValue)
        {
            query &= builder.Lt(x => x.Id, cursor.Value);
        }

        var items = await _context.Contracts.Find(query)
            .SortByDescending(x => x.Id)
            .Limit(limit + 1)
            .ToListAsync(cancellationToken);

        return (items.Take(limit).ToList(), items.Count > limit);
    }

    public async Task<Contract?> TryAttachEvidenceAsync(
        ObjectId id,
        ObjectId userId,
        DateTime now,
        int maxEvidence,
        CancellationToken cancellationToken)
    {
        var filter = Builders<Contract>.Filter.And(
            Builders<Contract>.Filter.Eq(x => x.Id, id),
            Builders<Contract>.Filter.Lt(x => x.EvidenceCount, maxEvidence),
            Builders<Contract>.Filter.In(x => x.Status, ["open", "accepted", "in_progress", "evidence_pending", "voting"]));

        var update = Builders<Contract>.Update
            .Inc(x => x.EvidenceCount, 1)
            .Set(x => x.UpdatedAt, now);

        var options = new FindOneAndUpdateOptions<Contract>
        {
            ReturnDocument = ReturnDocument.After
        };

        return await _context.Contracts.FindOneAndUpdateAsync(filter, update, options, cancellationToken);
    }

    public Task DecrementEvidenceAsync(ObjectId id, CancellationToken cancellationToken) =>
        _context.Contracts.UpdateOneAsync(
            x => x.Id == id && x.EvidenceCount > 0,
            Builders<Contract>.Update.Inc(x => x.EvidenceCount, -1),
            cancellationToken: cancellationToken);

    public Task MarkVotingAsync(
        ObjectId id,
        ObjectId executorId,
        IReadOnlyList<ObjectId> jurors,
        DateTime votingEndsAt,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        return _context.Contracts.UpdateOneAsync(
            x => x.Id == id && x.Status != "voting",
            Builders<Contract>.Update
                .Set(x => x.Status, "voting")
                .Set(x => x.ExecutorId, executorId)
                .Set(x => x.Jurors, jurors.ToList())
                .Set(x => x.Voters, new List<ObjectId>())
                .Set(x => x.VotingStartedAt, now)
                .Set(x => x.VotingEndsAt, votingEndsAt)
                .Set(x => x.UpdatedAt, now)
                .Inc(x => x.Version, 1),
            cancellationToken: cancellationToken);
    }

    public async Task<Contract?> TryRegisterVoteAsync(ObjectId id, ObjectId jurorId, bool legit, DateTime now, CancellationToken cancellationToken)
    {
        var builder = Builders<Contract>.Filter;
        var filter = builder.Eq(x => x.Id, id)
            & builder.Eq(x => x.Status, "voting")
            & builder.AnyEq(x => x.Jurors, jurorId)
            & builder.Not(builder.AnyEq(x => x.Voters, jurorId));
        var update = Builders<Contract>.Update
            .AddToSet(x => x.Voters, jurorId)
            .Inc(x => x.Version, 1)
            .Set(x => x.UpdatedAt, now);
        update = legit ? update.Inc(x => x.LegitVotes, 1) : update.Inc(x => x.CheatVotes, 1);

        return await _context.Contracts.FindOneAndUpdateAsync(
            filter,
            update,
            new FindOneAndUpdateOptions<Contract> { ReturnDocument = ReturnDocument.After },
            cancellationToken);
    }

    public async Task<(IReadOnlyList<Contract> Items, bool HasMore)> ListPendingVotesAsync(
        ObjectId jurorId,
        int limit,
        ObjectId? cursor,
        CancellationToken cancellationToken)
    {
        var builder = Builders<Contract>.Filter;
        var filter = builder.Eq(x => x.Status, "voting")
            & builder.AnyEq(x => x.Jurors, jurorId)
            & builder.Not(builder.AnyEq(x => x.Voters, jurorId));
        if (cursor.HasValue)
        {
            filter &= builder.Lt(x => x.Id, cursor.Value);
        }

        var items = await _context.Contracts.Find(filter).SortByDescending(x => x.Id).Limit(limit + 1).ToListAsync(cancellationToken);
        return (items.Take(limit).ToList(), items.Count > limit);
    }

    public async Task<(IReadOnlyList<Contract> Items, bool HasMore)> ListFulfilledAsync(
        ObjectId groupId,
        int limit,
        ObjectId? cursor,
        CancellationToken cancellationToken)
    {
        var builder = Builders<Contract>.Filter;
        var filter = builder.Eq(x => x.GroupId, groupId) & builder.Eq(x => x.Status, "fulfilled");
        if (cursor.HasValue)
        {
            filter &= builder.Lt(x => x.Id, cursor.Value);
        }

        var items = await _context.Contracts.Find(filter).SortByDescending(x => x.Id).Limit(limit + 1).ToListAsync(cancellationToken);
        return (items.Take(limit).ToList(), items.Count > limit);
    }

    public async Task<Contract?> ResolveAsync(ObjectId id, string outcome, string resolvedBy, DateTime now, CancellationToken cancellationToken)
    {
        using var session = await _context.Client.StartSessionAsync(cancellationToken: cancellationToken);
        session.StartTransaction();
        try
        {
            var contract = await _context.Contracts.FindOneAndUpdateAsync(
                session,
                Builders<Contract>.Filter.Where(x => x.Id == id && x.Status == "voting"),
                Builders<Contract>.Update
                    .Set(x => x.Status, outcome)
                    .Set(x => x.ResolvedBy, resolvedBy)
                    .Set(x => x.ResolvedAt, now)
                    .Set(x => x.UpdatedAt, now)
                    .Inc(x => x.Version, 1),
                new FindOneAndUpdateOptions<Contract> { ReturnDocument = ReturnDocument.After },
                cancellationToken);

            if (contract is null)
            {
                await session.AbortTransactionAsync(cancellationToken);
                return null;
            }

            var fulfilled = outcome == "fulfilled";
            if (contract.AcceptedBy is ObjectId acceptor && contract.BondRequired > 0)
            {
                // Cumplido: la fianza regresa a quien la puso. Trampa: la fianza pasa al retador.
                var receiver = fulfilled ? acceptor : contract.ChallengerId;
                var member = await _context.GroupMembers.FindOneAndUpdateAsync(
                    session,
                    Builders<GroupMember>.Filter.Where(x => x.GroupId == contract.GroupId && x.UserId == receiver),
                    Builders<GroupMember>.Update.Inc(x => x.PointsBalance, contract.BondRequired).Set(x => x.UpdatedAt, now),
                    new FindOneAndUpdateOptions<GroupMember> { ReturnDocument = ReturnDocument.After },
                    cancellationToken);

                if (member is not null)
                {
                    await _context.PointsLedger.InsertOneAsync(
                        session,
                        new PointsLedgerEntry
                        {
                            Id = ObjectId.GenerateNewId(),
                            GroupId = contract.GroupId,
                            UserId = receiver,
                            Type = fulfilled ? "bond_release" : "bond_forfeit",
                            Amount = contract.BondRequired,
                            RefType = "contract",
                            RefId = contract.Id,
                            BalanceAfter = member.PointsBalance,
                            CreatedAt = now
                        },
                        cancellationToken: cancellationToken);
                }
            }

            var executor = contract.ExecutorId ?? contract.AcceptedBy ?? contract.TargetId;
            if (executor is ObjectId executorId)
            {
                if (fulfilled && executorId != contract.ChallengerId)
                {
                    await _context.Commitments.InsertOneAsync(
                        session,
                        new Commitment
                        {
                            Id = ObjectId.GenerateNewId(),
                            GroupId = contract.GroupId,
                            ContractId = contract.Id,
                            DebtorId = contract.ChallengerId,
                            CreditorId = executorId,
                            Description = contract.Reward,
                            Game = contract.Game,
                            Status = "pending",
                            CreatedAt = now,
                            UpdatedAt = now
                        },
                        cancellationToken: cancellationToken);
                }

                var stats = fulfilled
                    ? Builders<User>.Update.Inc(x => x.Stats.Completed, 1).Inc(x => x.Stats.Streak, 1)
                    : Builders<User>.Update.Inc(x => x.Stats.Cheated, 1).Set(x => x.Stats.Streak, 0);
                await _context.Users.UpdateOneAsync(session, x => x.Id == executorId, stats.Set(x => x.UpdatedAt, now), cancellationToken: cancellationToken);
            }

            await session.CommitTransactionAsync(cancellationToken);
            return contract;
        }
        catch (MongoException ex) when (ex.HasErrorLabel("TransientTransactionError"))
        {
            if (session.IsInTransaction)
            {
                await session.AbortTransactionAsync(CancellationToken.None);
            }

            return null;
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

    public async Task<AcceptContractResult> AcceptWithBondAsync(
        ObjectId contractId,
        int expectedVersion,
        ObjectId userId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        // Atlas es un replica set: contrato, saldo y libro se escriben juntos o no se escribe nada.
        using var session = await _context.Client.StartSessionAsync(cancellationToken: cancellationToken);
        session.StartTransaction();
        try
        {
            var contract = await _context.Contracts.FindOneAndUpdateAsync(
                session,
                Builders<Contract>.Filter.Where(x => x.Id == contractId && x.Status == "open" && x.Version == expectedVersion),
                Builders<Contract>.Update
                    .Set(x => x.Status, "in_progress")
                    .Set(x => x.AcceptedBy, userId)
                    .Set(x => x.AcceptedAt, now)
                    .Set(x => x.UpdatedAt, now)
                    .Inc(x => x.Version, 1),
                new FindOneAndUpdateOptions<Contract> { ReturnDocument = ReturnDocument.After },
                cancellationToken);

            if (contract is null)
            {
                await session.AbortTransactionAsync(cancellationToken);
                return new AcceptContractResult(AcceptContractOutcome.StateChanged, null, 0);
            }

            var bond = contract.BondRequired;
            var member = await _context.GroupMembers.FindOneAndUpdateAsync(
                session,
                Builders<GroupMember>.Filter.Where(x =>
                    x.GroupId == contract.GroupId
                    && x.UserId == userId
                    && x.Status == "active"
                    && x.PointsBalance >= bond),
                Builders<GroupMember>.Update
                    .Inc(x => x.PointsBalance, -bond)
                    .Set(x => x.UpdatedAt, now),
                new FindOneAndUpdateOptions<GroupMember> { ReturnDocument = ReturnDocument.After },
                cancellationToken);

            if (member is null)
            {
                await session.AbortTransactionAsync(cancellationToken);
                return new AcceptContractResult(AcceptContractOutcome.InsufficientPoints, null, 0);
            }

            await _context.PointsLedger.InsertOneAsync(
                session,
                new PointsLedgerEntry
                {
                    Id = ObjectId.GenerateNewId(),
                    GroupId = contract.GroupId,
                    UserId = userId,
                    Type = "bond_lock",
                    Amount = -bond,
                    RefType = "contract",
                    RefId = contract.Id,
                    BalanceAfter = member.PointsBalance,
                    CreatedAt = now
                },
                cancellationToken: cancellationToken);

            await session.CommitTransactionAsync(cancellationToken);
            return new AcceptContractResult(AcceptContractOutcome.Accepted, contract, member.PointsBalance);
        }
        catch (MongoException ex) when (ex.HasErrorLabel("TransientTransactionError"))
        {
            // Otro usuario aceptó al mismo tiempo (conflicto de escritura): nada se aplicó.
            if (session.IsInTransaction)
            {
                await session.AbortTransactionAsync(CancellationToken.None);
            }

            return new AcceptContractResult(AcceptContractOutcome.StateChanged, null, 0);
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

    public async Task<Contract?> TryRejectAsync(
        ObjectId contractId,
        int expectedVersion,
        ObjectId userId,
        DateTime now,
        CancellationToken cancellationToken) =>
        await _context.Contracts.FindOneAndUpdateAsync(
            Builders<Contract>.Filter.Where(x => x.Id == contractId && x.Status == "open" && x.Version == expectedVersion),
            Builders<Contract>.Update
                .Set(x => x.Status, "cancelled")
                .Set(x => x.RejectedBy, userId)
                .Set(x => x.ClosedReason, "rejected")
                .Set(x => x.UpdatedAt, now)
                .Inc(x => x.Version, 1),
            new FindOneAndUpdateOptions<Contract> { ReturnDocument = ReturnDocument.After },
            cancellationToken);
}
