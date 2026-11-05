using Pactogg.Api.Common;
using Pactogg.Api.Common.Extensions;
using Pactogg.Api.Modules.Votes.Dtos;

namespace Pactogg.Api.Modules.Votes;

public static class VoteEndpoints
{
    public static IEndpointRouteBuilder MapVoteEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/me/votes/pending", async (
                int? limit,
                string? cursor,
                VoteService service,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var (items, meta) = await service.ListPendingAsync(context.User.GetRequiredObjectId(), limit ?? 20, cursor, cancellationToken);
                return ApiResults.Ok(items, meta);
            })
            .RequireAuthorization()
            .WithTags("Votes")
            .WithName("ListPendingVotes")
            .Produces<IReadOnlyList<PendingVoteResponse>>();

        var contracts = app.MapGroup("/api/v1/contracts/{id}").WithTags("Votes").RequireAuthorization();

        contracts.MapPut("/votes", async (string id, CastVoteRequest request, VoteService service, HttpContext context, CancellationToken cancellationToken) =>
                ApiResults.Ok(await service.CastAsync(context.User.GetRequiredObjectId(), id, request, cancellationToken)))
            .AddEndpointFilter<ValidationFilter<CastVoteRequest>>()
            .WithName("CastVote")
            .Produces<CastVoteResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        contracts.MapGet("/votes/summary", async (string id, VoteService service, HttpContext context, CancellationToken cancellationToken) =>
                ApiResults.Ok(await service.SummaryAsync(context.User.GetRequiredObjectId(), id, cancellationToken)))
            .WithName("GetVoteSummary")
            .Produces<VoteSummaryResponse>();

        contracts.MapPost("/votes/resolve", async (string id, ResolveVotingRequest request, VoteService service, HttpContext context, CancellationToken cancellationToken) =>
                ApiResults.Ok(await service.AdminResolveAsync(context.User.GetRequiredObjectId(), id, request, cancellationToken)))
            .AddEndpointFilter<ValidationFilter<ResolveVotingRequest>>()
            .WithName("ResolveVoting")
            .Produces<ContractResultResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        contracts.MapGet("/result", async (string id, VoteService service, HttpContext context, CancellationToken cancellationToken) =>
                ApiResults.Ok(await service.ResultAsync(context.User.GetRequiredObjectId(), id, cancellationToken)))
            .WithName("GetContractResult")
            .Produces<ContractResultResponse>()
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}
