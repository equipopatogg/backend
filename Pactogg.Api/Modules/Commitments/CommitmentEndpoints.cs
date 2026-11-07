using Pactogg.Api.Common;
using Pactogg.Api.Common.Extensions;
using Pactogg.Api.Modules.Commitments.Dtos;

namespace Pactogg.Api.Modules.Commitments;

public static class CommitmentEndpoints
{
    public static IEndpointRouteBuilder MapCommitmentEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/groups/{gid}/commitments", async (
                string gid,
                string? role,
                string? status,
                int? limit,
                string? cursor,
                CommitmentService service,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var (items, meta) = await service.ListAsync(context.User.GetRequiredObjectId(), gid, role, status, limit ?? 20, cursor, cancellationToken);
                return ApiResults.Ok(items, meta);
            })
            .RequireAuthorization()
            .WithTags("Commitments")
            .WithName("ListCommitments")
            .Produces<IReadOnlyList<CommitmentResponse>>()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        app.MapGet("/api/v1/groups/{gid}/commitments/summary", async (string gid, CommitmentService service, HttpContext context, CancellationToken cancellationToken) =>
                ApiResults.Ok(await service.SummaryAsync(context.User.GetRequiredObjectId(), gid, cancellationToken)))
            .WithTags("Commitments")
            .RequireAuthorization()
            .WithName("CommitmentSummary")
            .Produces<CommitmentSummaryResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        var group = app.MapGroup("/api/v1/commitments/{id}").WithTags("Commitments").RequireAuthorization();

        group.MapGet("", async (string id, CommitmentService service, HttpContext context, CancellationToken cancellationToken) =>
                ApiResults.Ok(await service.GetAsync(context.User.GetRequiredObjectId(), id, cancellationToken)))
            .WithName("GetCommitment")
            .Produces<CommitmentResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/complete", async (string id, CommitmentService service, HttpContext context, CancellationToken cancellationToken) =>
                ApiResults.Ok(await service.CompleteAsync(context.User.GetRequiredObjectId(), id, cancellationToken)))
            .WithName("CompleteCommitment")
            .Produces<CommitmentResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/request-complete", async (string id, CommitmentService service, HttpContext context, CancellationToken cancellationToken) =>
                ApiResults.Ok(await service.RequestCompleteAsync(context.User.GetRequiredObjectId(), id, cancellationToken)))
            .WithName("RequestCommitmentCompletion")
            .Produces<CommitmentResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}
