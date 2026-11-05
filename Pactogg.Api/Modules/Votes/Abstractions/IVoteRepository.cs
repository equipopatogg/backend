using MongoDB.Bson;
using Pactogg.Api.Modules.Votes.Domain;

namespace Pactogg.Api.Modules.Votes.Abstractions;

public interface IVoteRepository
{
    /// <summary>Inserta el voto; lanza 409 ALREADY_VOTED si el jurado ya votó (índice único).</summary>
    Task CreateAsync(Vote vote, CancellationToken cancellationToken);
    Task<IReadOnlyList<Vote>> ListByContractAsync(ObjectId contractId, CancellationToken cancellationToken);
}
