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
            .WithName("CreateContract")
            .Produces<ContractResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

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
            .WithName("CreateGroupContract")
            .Produces<ContractResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }
}
