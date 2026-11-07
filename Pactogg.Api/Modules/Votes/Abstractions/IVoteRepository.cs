using MongoDB.Bson;
using Pactogg.Api.Modules.Votes.Domain;

namespace Pactogg.Api.Modules.Votes.Abstractions;

public interface IVoteRepository
{
    /// <summary>Inserta el voto; lanza 409 ALREADY_VOTED si el jurado ya votó (índice único).</summary>
    Task CreateAsync(Vote vote, CancellationToken cancellationToken);
    Task<IReadOnlyList<Vote>> ListByContractAsync(ObjectId contractId, CancellationToken cancellationToken);

    Task<Vote?> GetByIdAsync(ObjectId id, CancellationToken cancellationToken);

    /// <summary>REP-03/04: borra el comentario de un voto; el voto se conserva.</summary>
    Task ClearCommentAsync(ObjectId id, CancellationToken cancellationToken);

    /// <summary>USR-04: los comentarios de voto de la persona se borran (CLARIFICACION #15).</summary>
    Task ClearCommentsByJurorAsync(ObjectId jurorId, CancellationToken cancellationToken);
}
