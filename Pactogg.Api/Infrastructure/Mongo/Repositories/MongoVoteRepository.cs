using MongoDB.Bson;
using MongoDB.Driver;
using Pactogg.Api.Common;
using Pactogg.Api.Modules.Votes.Abstractions;
using Pactogg.Api.Modules.Votes.Domain;

namespace Pactogg.Api.Infrastructure.Mongo.Repositories;

public sealed class MongoVoteRepository : IVoteRepository
{
    private readonly MongoContext _context;

    public MongoVoteRepository(MongoContext context)
    {
        _context = context;
    }

    public async Task CreateAsync(Vote vote, CancellationToken cancellationToken)
    {
        try
        {
            await _context.Votes.InsertOneAsync(vote, cancellationToken: cancellationToken);
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            throw ApiException.Conflict(ErrorCodes.AlreadyVoted, "Ya votaste en este reto.");
        }
    }

    public async Task<IReadOnlyList<Vote>> ListByContractAsync(ObjectId contractId, CancellationToken cancellationToken) =>
        await _context.Votes.Find(x => x.ContractId == contractId).SortBy(x => x.CreatedAt).ToListAsync(cancellationToken);
}
