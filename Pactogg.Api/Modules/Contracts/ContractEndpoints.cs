using Pactogg.Api.Common.Middleware;
using Pactogg.Api.Common;
using Pactogg.Api.Common.Extensions;
using Pactogg.Api.Modules.Contracts.Dtos;

namespace Pactogg.Api.Modules.Contracts;

public static class ContractEndpoints
{
    public static IEndpointRouteBuilder MapContractEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/contracts").WithTags("Contracts");

        group.MapPost("", async (
                CreateContractRequest request,
                ContractService service,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var response = await service.CreateAsync(context.User.GetRequiredObjectId(), request, cancellationToken);
                return ApiResults.Created(response);
            })
            .RequireAuthorization()
            .AddEndpointFilter<ValidationFilter<CreateContractRequest>>()
            .AllowIdempotencyKey()
            .WithName("CreateContract")
            .Produces<ContractResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapGet("/{id}", async (
                string id,
                ContractService service,
                HttpContext context,
                CancellationToken cancellationToken) =>
                ApiResults.Ok(await service.GetAsync(context.User.GetRequiredObjectId(), id, cancellationToken)))
            .RequireAuthorization()
            .WithName("GetContract")
            .Produces<ContractResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id}/accept", async (
                string id,
                ContractService service,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var request = await ReadOptionalActionAsync(context.Request, cancellationToken);
                return ApiResults.Ok(await service.AcceptAsync(context.User.GetRequiredObjectId(), id, request, cancellationToken));
            })
            .RequireAuthorization()
            .RequireIdempotencyKey()
            .WithName("AcceptContract")
            .Produces<ContractResponse>()
            .ProducesProblem(StatusCodes.Status402PaymentRequired)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id}/reject", async (
                string id,
                ContractService service,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var request = await ReadOptionalActionAsync(context.Request, cancellationToken);
                return ApiResults.Ok(await service.RejectAsync(context.User.GetRequiredObjectId(), id, request, cancellationToken));
            })
            .RequireAuthorization()
            .WithName("RejectContract")
            .Produces<ContractResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPatch("/{id}", async (
                string id,
                UpdateContractRequest request,
                ContractService service,
                HttpContext context,
                CancellationToken cancellationToken) =>
                ApiResults.Ok(await service.UpdateAsync(context.User.GetRequiredObjectId(), id, request, cancellationToken)))
            .RequireAuthorization()
            .AddEndpointFilter<ValidationFilter<UpdateContractRequest>>()
            .WithName("UpdateContract")
            .Produces<ContractResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapDelete("/{id}", async (
                string id,
                int? version,
                ContractService service,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                await service.CancelAsync(context.User.GetRequiredObjectId(), id, version, cancellationToken);
                return Results.NoContent();
            })
            .RequireAuthorization()
            .WithName("CancelContract")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        var nested = app.MapGroup("/api/v1/groups/{gid}/contracts").WithTags("Contracts");
        nested.MapPost("", async (
                string gid,
                CreateContractRequest request,
                ContractService service,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                if (!string.IsNullOrWhiteSpace(request.GroupId)
                    && !string.Equals(request.GroupId, gid, StringComparison.Ordinal))
                {
                    throw ApiException.Unprocessable(ErrorCodes.ValidationError, "group_id no coincide con la ruta.");
                }

                request.GroupId = gid;
                var response = await service.CreateAsync(context.User.GetRequiredObjectId(), request, cancellationToken);
                return ApiResults.Created(response);
            })
            .RequireAuthorization()
            .AddEndpointFilter<ValidationFilter<CreateContractRequest>>()
            .AllowIdempotencyKey()
            .WithName("CreateGroupContract")
            .Produces<ContractResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        nested.MapGet("", async (
                string gid,
                string? status,
                bool? mine,
                string? game,
                string? q,
                int? limit,
                string? cursor,
                ContractService service,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var (items, meta) = await service.ListAsync(
                    context.User.GetRequiredObjectId(),
                    gid,
                    status,
                    mine ?? false,
                    game,
                    q,
                    limit ?? 20,
                    cursor,
                    cancellationToken);
                return ApiResults.Ok(items, meta);
            })
            .RequireAuthorization()
            .WithName("ListGroupContracts")
            .Produces<IReadOnlyList<ContractResponse>>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }

    /// <summary>El cuerpo de accept/reject es opcional ({"version": n}); sin cuerpo se acepta la versión actual.</summary>
    private static async Task<ContractActionRequest?> ReadOptionalActionAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (request.ContentLength == 0 || !request.HasJsonContentType())
        {
            if (request.ContentLength is > 0)
            {
                throw ApiException.BadRequest(ErrorCodes.InvalidInput, "El cuerpo debe ser JSON.");
            }

            return null;
        }

        return await request.ReadFromJsonAsync<ContractActionRequest>(cancellationToken);
    }
}
