using System.Text.RegularExpressions;
using MongoDB.Bson;
using MongoDB.Driver;
using Pactogg.Api.Modules.Contracts.Abstractions;
using Pactogg.Api.Modules.Contracts.Domain;
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

    public Task MarkVotingAsync(ObjectId id, CancellationToken cancellationToken) =>
        _context.Contracts.UpdateOneAsync(
            x => x.Id == id && x.Status != "voting",
            Builders<Contract>.Update
                .Set(x => x.Status, "voting")
                .Set(x => x.UpdatedAt, DateTime.UtcNow),
            cancellationToken: cancellationToken);

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
