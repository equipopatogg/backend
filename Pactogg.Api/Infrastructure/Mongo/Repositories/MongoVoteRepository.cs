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

    public async Task<Vote?> GetByIdAsync(ObjectId id, CancellationToken cancellationToken) =>
        await _context.Votes.Find(x => x.Id == id).FirstOrDefaultAsync(cancellationToken);

    public Task ClearCommentAsync(ObjectId id, CancellationToken cancellationToken) =>
        _context.Votes.UpdateOneAsync(x => x.Id == id, Builders<Vote>.Update.Set(x => x.Comment, null), cancellationToken: cancellationToken);

    public Task ClearCommentsByJurorAsync(ObjectId jurorId, CancellationToken cancellationToken) =>
        _context.Votes.UpdateManyAsync(x => x.JurorId == jurorId && x.Comment != null, Builders<Vote>.Update.Set(x => x.Comment, null), cancellationToken: cancellationToken);
}
