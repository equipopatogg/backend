using Pactogg.Api.Common;
using Pactogg.Api.Common.Extensions;
using Microsoft.Extensions.Options;
using Pactogg.Api.Common.Middleware;
using Pactogg.Api.Infrastructure.Options;
using Pactogg.Api.Modules.Tournaments.Dtos;

namespace Pactogg.Api.Modules.Tournaments;

/// <summary>Spec 010: TOR-01..09 y MAT-01..07.</summary>
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
        // TOR-09 (010-R8).
        group.MapPost("/{id}/invite", async (string id, InviteGroupsRequest request, MatchService service, HttpContext context, CancellationToken cancellationToken) =>
                ApiResults.Ok(await service.InviteAsync(context.User.GetRequiredObjectId(), id, request, cancellationToken)))
            .RequireAuthorization()
            .AddEndpointFilter<ValidationFilter<InviteGroupsRequest>>()
            .WithName("InviteGroupsToTournament")
            .Produces<InviteGroupsResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        var matches = app.MapGroup("/api/v1/matches").WithTags("Matches");

        matches.MapGet("/{id}", async (string id, MatchService service, HttpContext context, CancellationToken cancellationToken) =>
                ApiResults.Ok(await service.GetAsync(context.User.GetRequiredObjectId(), id, cancellationToken)))
            .RequireAuthorization()
            .WithName("GetMatch")
            .Produces<MatchResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // MAT-03 (010-R10): multipart con file, p1_score, p2_score y, opcionales, winner_group_id y group_id.
        matches.MapPost("/{id}/report", async (string id, MatchService service, IOptions<EvidenceOptions> options, HttpContext context, CancellationToken cancellationToken) =>
            {
                if (!context.Request.HasFormContentType)
                {
                    throw ApiException.Unprocessable(ErrorCodes.ValidationError, "Envía multipart/form-data con file, p1_score y p2_score.");
                }

                IFormCollection form;
                try
                {
                    form = await context.Request.ReadFormAsync(cancellationToken);
                }
                catch (InvalidDataException)
                {
                    throw ApiException.Unprocessable(ErrorCodes.ImageTooLarge, "La imagen supera el tamaño permitido.");
                }

                if (!int.TryParse(form["p1_score"], out var p1) || !int.TryParse(form["p2_score"], out var p2))
                {
                    throw ApiException.Unprocessable(ErrorCodes.ValidationError, "p1_score y p2_score son números enteros.");
                }

                var file = form.Files.GetFile("file")
                    ?? throw ApiException.Unprocessable(ErrorCodes.ValidationError, "Falta la foto del resultado (file).");
                if (file.Length <= 0 || file.Length > options.Value.MaxImageBytes)
                {
                    throw ApiException.Unprocessable(ErrorCodes.ImageTooLarge, "La imagen supera el tamaño permitido.");
                }

                await using var stream = file.OpenReadStream();
                using var memory = new MemoryStream();
                await stream.CopyToAsync(memory, cancellationToken);
                var winner = form["winner_group_id"].ToString();
                var groupId = form["group_id"].ToString();
                return ApiResults.Ok(await service.ReportAsync(
                    context.User.GetRequiredObjectId(),
                    id,
                    p1,
                    p2,
                    string.IsNullOrWhiteSpace(groupId) ? null : groupId,
                    string.IsNullOrWhiteSpace(winner) ? null : winner,
                    memory.ToArray(),
                    file.ContentType,
                    cancellationToken));
            })
            .RequireAuthorization()
            .WithName("ReportMatch")
            .Produces<MatchResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        // MAT-04 (010-R11).
        matches.MapPost("/{id}/confirm", async (string id, MatchService service, HttpContext context, CancellationToken cancellationToken) =>
                ApiResults.Ok(await service.ConfirmAsync(context.User.GetRequiredObjectId(), id, cancellationToken)))
            .RequireAuthorization()
            .WithName("ConfirmMatch")
            .Produces<MatchResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // MAT-05 (010-R12).
        matches.MapPost("/{id}/dispute", async (string id, DisputeMatchRequest request, MatchService service, HttpContext context, CancellationToken cancellationToken) =>
                ApiResults.Ok(await service.DisputeAsync(context.User.GetRequiredObjectId(), id, request, cancellationToken)))
            .RequireAuthorization()
            .AddEndpointFilter<ValidationFilter<DisputeMatchRequest>>()
            .WithName("DisputeMatch")
            .Produces<MatchResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // MAT-06 (010-R13).
        matches.MapPost("/{id}/resolve", async (string id, ResolveMatchRequest request, MatchService service, HttpContext context, CancellationToken cancellationToken) =>
                ApiResults.Ok(await service.ResolveAsync(context.User.GetRequiredObjectId(), id, request, cancellationToken)))
            .RequireAuthorization()
            .AddEndpointFilter<ValidationFilter<ResolveMatchRequest>>()
            .WithName("ResolveMatch")
            .Produces<MatchResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

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
