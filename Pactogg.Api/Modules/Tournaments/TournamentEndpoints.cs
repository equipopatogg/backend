using Pactogg.Api.Common;
using Pactogg.Api.Common.Extensions;
using Pactogg.Api.Common.Middleware;
using Pactogg.Api.Modules.Tournaments.Dtos;

namespace Pactogg.Api.Modules.Tournaments;

/// <summary>Spec 010: TOR-01..08 y las lecturas MAT-01, MAT-02 y MAT-07.</summary>
public static class TournamentEndpoints
{
    public static IEndpointRouteBuilder MapTournamentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/tournaments").WithTags("Tournaments");

        // TOR-02 (todas mis torneos).
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
                var (items, meta) = await service.ListAsync(context.User.GetRequiredObjectId(), status, game, group_id, limit ?? 20, cursor, cancellationToken);
                return ApiResults.Ok(items, meta);
            })
            .RequireAuthorization()
            .WithName("ListTournaments")
            .Produces<IReadOnlyList<TournamentResponse>>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        // TOR-03: {id} acepta el id o el código de 6 caracteres.
        group.MapGet("/{id}", async (string id, TournamentService service, HttpContext context, CancellationToken cancellationToken) =>
                ApiResults.Ok(await service.GetAsync(context.User.GetRequiredObjectId(), id, cancellationToken)))
            .RequireAuthorization()
            .WithName("GetTournament")
            .Produces<TournamentResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // TOR-04.
        group.MapPatch("/{id}", async (string id, UpdateTournamentRequest request, TournamentService service, HttpContext context, CancellationToken cancellationToken) =>
                ApiResults.Ok(await service.UpdateAsync(context.User.GetRequiredObjectId(), id, request, cancellationToken)))
            .RequireAuthorization()
            .AddEndpointFilter<ValidationFilter<UpdateTournamentRequest>>()
            .WithName("UpdateTournament")
            .Produces<TournamentResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        // TOR-05.
        group.MapPost("/{id}/cancel", async (string id, TournamentService service, HttpContext context, CancellationToken cancellationToken) =>
                ApiResults.Ok(await service.CancelAsync(context.User.GetRequiredObjectId(), id, cancellationToken)))
            .RequireAuthorization()
            .WithName("CancelTournament")
            .Produces<TournamentResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // TOR-06: {id} acepta el id (con code en el cuerpo) o el código.
        group.MapPost("/{id}/join", async (string id, JoinTournamentRequest request, TournamentService service, HttpContext context, CancellationToken cancellationToken) =>
                ApiResults.Ok(await service.JoinAsync(context.User.GetRequiredObjectId(), id, request, cancellationToken)))
            .RequireAuthorization()
            .RequireIdempotencyKey()
            .AddEndpointFilter<ValidationFilter<JoinTournamentRequest>>()
            .WithName("JoinTournament")
            .Produces<TournamentResponse>()
            .ProducesProblem(StatusCodes.Status402PaymentRequired)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // TOR-07.
        group.MapDelete("/{id}/join", async (string id, string? group_id, TournamentService service, HttpContext context, CancellationToken cancellationToken) =>
            {
                await service.LeaveAsync(context.User.GetRequiredObjectId(), id, group_id, cancellationToken);
                return Results.NoContent();
            })
            .RequireAuthorization()
            .WithName("LeaveTournament")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // TOR-08.
        group.MapPost("/{id}/start", async (string id, TournamentService service, HttpContext context, CancellationToken cancellationToken) =>
                ApiResults.Ok(await service.StartAsync(context.User.GetRequiredObjectId(), id, cancellationToken)))
            .RequireAuthorization()
            .WithName("StartTournament")
            .Produces<TournamentResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // MAT-01.
        group.MapGet("/{id}/bracket", async (string id, TournamentService service, HttpContext context, CancellationToken cancellationToken) =>
                ApiResults.Ok(await service.GetBracketAsync(context.User.GetRequiredObjectId(), id, cancellationToken)))
            .RequireAuthorization()
            .WithTags("Matches")
            .WithName("GetTournamentBracket")
            .Produces<BracketResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        var nested = app.MapGroup("/api/v1/groups/{gid}/tournaments").WithTags("Tournaments");

        // TOR-02.
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
                var (items, meta) = await service.ListAsync(context.User.GetRequiredObjectId(), status, game, gid, limit ?? 20, cursor, cancellationToken);
                return ApiResults.Ok(items, meta);
            })
            .RequireAuthorization()
            .WithName("ListGroupTournaments")
            .Produces<IReadOnlyList<TournamentResponse>>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        // TOR-01.
        nested.MapPost("", async (string gid, CreateTournamentRequest request, TournamentService service, HttpContext context, CancellationToken cancellationToken) =>
                ApiResults.Created(await service.CreateAsync(context.User.GetRequiredObjectId(), gid, request, cancellationToken)))
            .RequireAuthorization()
            .AddEndpointFilter<ValidationFilter<CreateTournamentRequest>>()
            .WithName("CreateTournament")
            .Produces<TournamentResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status402PaymentRequired)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        // MAT-02.
        app.MapGet("/api/v1/matches/{id}", async (string id, TournamentService service, HttpContext context, CancellationToken cancellationToken) =>
                ApiResults.Ok(await service.GetMatchAsync(context.User.GetRequiredObjectId(), id, cancellationToken)))
            .RequireAuthorization()
            .WithTags("Matches")
            .WithName("GetMatch")
            .Produces<MatchResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // MAT-07: data es null si no hay partida pendiente.
        app.MapGet("/api/v1/me/matches/next", async (TournamentService service, HttpContext context, CancellationToken cancellationToken) =>
            {
                var next = await service.GetNextMatchAsync(context.User.GetRequiredObjectId(), cancellationToken);
                return next is null
                    ? Results.Content("{\"data\":null}", "application/json")
                    : ApiResults.Ok(next);
            })
            .RequireAuthorization()
            .WithTags("Matches")
            .WithName("GetMyNextMatch")
            .Produces<NextMatchResponse>();

        return app;
    }
}
