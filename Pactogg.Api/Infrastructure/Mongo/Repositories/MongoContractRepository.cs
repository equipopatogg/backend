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

    public async Task<Contract?> TryRevertToInProgressAsync(ObjectId id, DateTime now, CancellationToken cancellationToken)
    {
        var builder = Builders<Contract>.Filter;
        var filter = builder.Eq(x => x.Id, id)
            & builder.Eq(x => x.Status, "voting")
            & builder.Size(x => x.Voters, 0)
            & builder.Eq(x => x.AdminDeadlineAt, null)
            & builder.Lte(x => x.EvidenceCount, 0);
        var update = Builders<Contract>.Update
            .Set(x => x.Status, "in_progress")
            .Set(x => x.Jurors, new List<ObjectId>())
            .Set(x => x.VotingStartedAt, null)
            .Set(x => x.VotingEndsAt, null)
            .Set(x => x.VotingExtended, false)
            .Set(x => x.ExtraJurorAssigned, false)
            .Set(x => x.VoteReminderSent, false)
            .Set(x => x.UpdatedAt, now)
            .Inc(x => x.Version, 1);
        return await _context.Contracts.FindOneAndUpdateAsync(
            filter,
            update,
            new FindOneAndUpdateOptions<Contract> { ReturnDocument = ReturnDocument.After },
            cancellationToken);
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

    public async Task<(IReadOnlyList<Contract> Items, bool HasMore)> ListShameAsync(ObjectId groupId, int limit, ObjectId? cursor, CancellationToken cancellationToken)
    {
        var builder = Builders<Contract>.Filter;
        var filter = builder.Eq(x => x.GroupId, groupId) & builder.In(x => x.Status, ["cheating", "expired"]);
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
        return await InTransactionAsync(async session =>
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
                return null;
            }

            var fulfilled = outcome == "fulfilled";
            if (contract.AcceptedBy is ObjectId acceptor && contract.BondRequired > 0)
            {
                if (fulfilled)
                {
                    // CLARIFICACION #89: se libera la fianza y el retado recibe un premio igual a la fianza.
                    await CreditAsync(session, contract, acceptor, contract.BondRequired, "bond_release", now, cancellationToken);
                    await CreditAsync(session, contract, acceptor, contract.BondRequired, "reward", now, cancellationToken);
                }
                else
                {
                    await CreditAsync(session, contract, contract.ChallengerId, contract.BondRequired, "bond_forfeit", now, cancellationToken);
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

            return contract;
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<Contract>> ListPastDeadlineAsync(DateTime now, int limit, CancellationToken cancellationToken) =>
        await _context.Contracts
            .Find(Builders<Contract>.Filter.In(x => x.Status, ["open", "in_progress"]) & Builders<Contract>.Filter.Lte(x => x.Deadline, now))
            .Limit(limit)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Contract>> ListVotingDueAsync(DateTime now, DateTime reminderThreshold, int limit, CancellationToken cancellationToken)
    {
        var builder = Builders<Contract>.Filter;
        var due = builder.Lte(x => x.VotingEndsAt, now)
            | builder.Lte(x => x.AdminDeadlineAt, now)
            | (builder.Lte(x => x.VotingEndsAt, reminderThreshold) & builder.Ne(x => x.VoteReminderSent, true));
        return await _context.Contracts.Find(builder.Eq(x => x.Status, "voting") & due).Limit(limit).ToListAsync(cancellationToken);
    }

    public async Task<Contract?> TryExpireAsync(ObjectId id, DateTime now, CancellationToken cancellationToken)
    {
        return await InTransactionAsync(async session =>
        {
            var contract = await _context.Contracts.FindOneAndUpdateAsync(
                session,
                Builders<Contract>.Filter.In(x => x.Status, ["open", "in_progress"])
                    & Builders<Contract>.Filter.Eq(x => x.Id, id)
                    & Builders<Contract>.Filter.Lte(x => x.Deadline, now),
                Builders<Contract>.Update
                    .Set(x => x.Status, "expired")
                    .Set(x => x.ClosedReason, "deadline_without_evidence")
                    .Set(x => x.ResolvedBy, "system")
                    .Set(x => x.ResolvedAt, now)
                    .Set(x => x.UpdatedAt, now)
                    .Inc(x => x.Version, 1),
                new FindOneAndUpdateOptions<Contract> { ReturnDocument = ReturnDocument.After },
                cancellationToken);

            // CLARIFICACION #36: vence sin evidencia → el retado pierde la fianza a favor del retador.
            if (contract is { AcceptedBy: not null, BondRequired: > 0 })
            {
                await CreditAsync(session, contract, contract.ChallengerId, contract.BondRequired, "bond_forfeit", now, cancellationToken);
                await _context.Users.UpdateOneAsync(session, x => x.Id == contract.AcceptedBy, Builders<User>.Update.Set(x => x.Stats.Streak, 0), cancellationToken: cancellationToken);
            }

            return contract;
        }, cancellationToken);
    }

    public async Task<Contract?> TryExtendVotingAsync(ObjectId id, DateTime newEnd, DateTime now, CancellationToken cancellationToken) =>
        await _context.Contracts.FindOneAndUpdateAsync(
            Builders<Contract>.Filter.Where(x => x.Id == id && x.Status == "voting" && !x.VotingExtended),
            Builders<Contract>.Update
                .Set(x => x.VotingExtended, true)
                .Set(x => x.VotingEndsAt, newEnd)
                .Set(x => x.VoteReminderSent, false)
                .Set(x => x.UpdatedAt, now)
                .Inc(x => x.Version, 1),
            new FindOneAndUpdateOptions<Contract> { ReturnDocument = ReturnDocument.After },
            cancellationToken);

    public async Task<Contract?> TryStartAdminPhaseAsync(ObjectId id, DateTime adminDeadline, DateTime now, CancellationToken cancellationToken) =>
        await _context.Contracts.FindOneAndUpdateAsync(
            Builders<Contract>.Filter.Where(x => x.Id == id && x.Status == "voting" && x.AdminDeadlineAt == null),
            Builders<Contract>.Update
                .Set(x => x.AdminDeadlineAt, adminDeadline)
                .Set(x => x.UpdatedAt, now)
                .Inc(x => x.Version, 1),
            new FindOneAndUpdateOptions<Contract> { ReturnDocument = ReturnDocument.After },
            cancellationToken);

    public async Task<Contract?> TryAddExtraJurorAsync(ObjectId id, ObjectId jurorId, DateTime now, CancellationToken cancellationToken) =>
        await _context.Contracts.FindOneAndUpdateAsync(
            Builders<Contract>.Filter.Where(x => x.Id == id && x.Status == "voting" && !x.ExtraJurorAssigned),
            Builders<Contract>.Update
                .AddToSet(x => x.Jurors, jurorId)
                .Set(x => x.ExtraJurorAssigned, true)
                .Set(x => x.VotingEndsAt, now.AddHours(24))
                .Set(x => x.UpdatedAt, now)
                .Inc(x => x.Version, 1),
            new FindOneAndUpdateOptions<Contract> { ReturnDocument = ReturnDocument.After },
            cancellationToken);

    public Task MarkReminderSentAsync(ObjectId id, CancellationToken cancellationToken) =>
        _context.Contracts.UpdateOneAsync(
            x => x.Id == id,
            Builders<Contract>.Update.Set(x => x.VoteReminderSent, true),
            cancellationToken: cancellationToken);

    public async Task<Contract?> TryCancelVotingWithRefundAsync(ObjectId id, string reason, DateTime now, CancellationToken cancellationToken)
    {
        return await InTransactionAsync(async session =>
        {
            var contract = await _context.Contracts.FindOneAndUpdateAsync(
                session,
                Builders<Contract>.Filter.Where(x => x.Id == id && x.Status == "voting"),
                Builders<Contract>.Update
                    .Set(x => x.Status, "cancelled")
                    .Set(x => x.ClosedReason, reason)
                    .Set(x => x.ResolvedBy, "system")
                    .Set(x => x.ResolvedAt, now)
                    .Set(x => x.UpdatedAt, now)
                    .Inc(x => x.Version, 1),
                new FindOneAndUpdateOptions<Contract> { ReturnDocument = ReturnDocument.After },
                cancellationToken);

            if (contract is { AcceptedBy: ObjectId acceptor, BondRequired: > 0 })
            {
                await CreditAsync(session, contract, acceptor, contract.BondRequired, "bond_release", now, cancellationToken);
            }

            return contract;
        }, cancellationToken);
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

    public async Task<Contract?> TryUpdateOpenAsync(ObjectId id, int expectedVersion, string game, string condition, string reward, DateTime deadline, int bond, DateTime now, CancellationToken cancellationToken) =>
        await _context.Contracts.FindOneAndUpdateAsync(
            Builders<Contract>.Filter.Where(x => x.Id == id && x.Status == "open" && x.Version == expectedVersion && x.AcceptedBy == null),
            Builders<Contract>.Update
                .Set(x => x.Game, game)
                .Set(x => x.Condition, condition)
                .Set(x => x.Reward, reward)
                .Set(x => x.Deadline, deadline)
                .Set(x => x.BondRequired, bond)
                .Set(x => x.UpdatedAt, now)
                .Inc(x => x.Version, 1),
            new FindOneAndUpdateOptions<Contract> { ReturnDocument = ReturnDocument.After },
            cancellationToken);

    public async Task<Contract?> TryCancelOpenAsync(ObjectId id, int expectedVersion, DateTime now, CancellationToken cancellationToken) =>
        await _context.Contracts.FindOneAndUpdateAsync(
            Builders<Contract>.Filter.Where(x => x.Id == id && x.Status == "open" && x.Version == expectedVersion && x.AcceptedBy == null),
            Builders<Contract>.Update
                .Set(x => x.Status, "cancelled")
                .Set(x => x.ClosedReason, "cancelled_by_challenger")
                .Set(x => x.ResolvedAt, now)
                .Set(x => x.UpdatedAt, now)
                .Inc(x => x.Version, 1),
            new FindOneAndUpdateOptions<Contract> { ReturnDocument = ReturnDocument.After },
            cancellationToken);

    /// <summary>Suma puntos al miembro y registra el movimiento inmutable, dentro de la misma transacción.</summary>
    private async Task CreditAsync(
        IClientSessionHandle session,
        Contract contract,
        ObjectId userId,
        int amount,
        string type,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var member = await _context.GroupMembers.FindOneAndUpdateAsync(
            session,
            Builders<GroupMember>.Filter.Where(x => x.GroupId == contract.GroupId && x.UserId == userId),
            Builders<GroupMember>.Update.Inc(x => x.PointsBalance, amount).Set(x => x.UpdatedAt, now),
            new FindOneAndUpdateOptions<GroupMember> { ReturnDocument = ReturnDocument.After },
            cancellationToken);

        if (member is null)
        {
            return;
        }

        await _context.PointsLedger.InsertOneAsync(
            session,
            new PointsLedgerEntry
            {
                Id = ObjectId.GenerateNewId(),
                GroupId = contract.GroupId,
                UserId = userId,
                Type = type,
                Amount = amount,
                RefType = "contract",
                RefId = contract.Id,
                BalanceAfter = member.PointsBalance,
                CreatedAt = now
            },
            cancellationToken: cancellationToken);
    }

    /// <summary>Ejecuta en transacción; un resultado null aborta. Conflicto de escritura → null.</summary>
    private async Task<Contract?> InTransactionAsync(Func<IClientSessionHandle, Task<Contract?>> work, CancellationToken cancellationToken)
    {
        using var session = await _context.Client.StartSessionAsync(cancellationToken: cancellationToken);
        session.StartTransaction();
        try
        {
            var result = await work(session);
            if (result is null)
            {
                await session.AbortTransactionAsync(cancellationToken);
                return null;
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

    private static readonly string[] ActiveStatuses = ["open", "in_progress", "voting"];

    public async Task<IReadOnlyList<Contract>> ListActiveAsync(ObjectId groupId, ObjectId? userId, CancellationToken cancellationToken)
    {
        var builder = Builders<Contract>.Filter;
        var filter = builder.Eq(x => x.GroupId, groupId) & builder.In(x => x.Status, ActiveStatuses);
        if (userId is ObjectId uid)
        {
            filter &= builder.Or(
                builder.Eq(x => x.ChallengerId, uid),
                builder.Eq(x => x.AcceptedBy, uid),
                builder.Eq(x => x.TargetId, uid));
        }

        return await _context.Contracts.Find(filter).Limit(200).ToListAsync(cancellationToken);
    }

    public async Task<Contract?> TryCancelByAdminAsync(ObjectId id, DateTime now, CancellationToken cancellationToken)
    {
        return await InTransactionAsync(async session =>
        {
            var contract = await _context.Contracts.FindOneAndUpdateAsync(
                session,
                Builders<Contract>.Filter.Eq(x => x.Id, id) & Builders<Contract>.Filter.In(x => x.Status, ["open", "in_progress"]),
                Builders<Contract>.Update
                    .Set(x => x.Status, "cancelled")
                    .Set(x => x.ClosedReason, "cancelled_by_admin")
                    .Set(x => x.ResolvedBy, "admin")
                    .Set(x => x.ResolvedAt, now)
                    .Set(x => x.UpdatedAt, now)
                    .Inc(x => x.Version, 1),
                new FindOneAndUpdateOptions<Contract> { ReturnDocument = ReturnDocument.After },
                cancellationToken);

            if (contract is { AcceptedBy: ObjectId acceptor, BondRequired: > 0 })
            {
                await CreditAsync(session, contract, acceptor, contract.BondRequired, "bond_release", now, cancellationToken);
            }

            return contract;
        }, cancellationToken);
    }

    public async Task<Contract?> TryCancelActiveWithRefundAsync(ObjectId id, string reason, DateTime now, CancellationToken cancellationToken)
    {
        return await InTransactionAsync(async session =>
        {
            var contract = await _context.Contracts.FindOneAndUpdateAsync(
                session,
                Builders<Contract>.Filter.Eq(x => x.Id, id) & Builders<Contract>.Filter.In(x => x.Status, ActiveStatuses),
                Builders<Contract>.Update
                    .Set(x => x.Status, "cancelled")
                    .Set(x => x.ClosedReason, reason)
                    .Set(x => x.ResolvedBy, "system")
                    .Set(x => x.ResolvedAt, now)
                    .Set(x => x.UpdatedAt, now)
                    .Inc(x => x.Version, 1),
                new FindOneAndUpdateOptions<Contract> { ReturnDocument = ReturnDocument.After },
                cancellationToken);

            if (contract is { AcceptedBy: ObjectId acceptor, BondRequired: > 0 })
            {
                await CreditAsync(session, contract, acceptor, contract.BondRequired, "bond_release", now, cancellationToken);
            }

            return contract;
        }, cancellationToken);
    }
}
