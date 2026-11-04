using Pactogg.Api.Common;
using Pactogg.Api.Common.Extensions;

namespace Pactogg.Api.Modules.Tournaments;

public static class TournamentEndpoints
{
    public static IEndpointRouteBuilder MapTournamentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/tournaments").WithTags("Tournaments");

        group.MapGet("", async (
                string? status,
                string? game,
                string? group_id,
                int? limit,
                string? cursor,
                TournamentService service,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var (items, meta) = await service.ListAsync(
                    context.User.GetRequiredObjectId(),
                    status,
                    game,
                    group_id,
                    limit ?? 20,
                    cursor,
                    cancellationToken);
                return ApiResults.Ok(items, meta);
            })
            .RequireAuthorization()
            .WithName("ListTournaments")
            .Produces<IReadOnlyList<Modules.Tournaments.Dtos.TournamentResponse>>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        var nested = app.MapGroup("/api/v1/groups/{gid}/tournaments").WithTags("Tournaments");
        nested.MapGet("", async (
                string gid,
                string? status,
                string? game,
                int? limit,
                string? cursor,
                TournamentService service,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var (items, meta) = await service.ListAsync(
                    context.User.GetRequiredObjectId(),
                    status,
                    game,
                    gid,
                    limit ?? 20,
                    cursor,
                    cancellationToken);
                return ApiResults.Ok(items, meta);
            })
            .RequireAuthorization()
            .WithName("ListGroupTournaments")
            .Produces<IReadOnlyList<Modules.Tournaments.Dtos.TournamentResponse>>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }
}
