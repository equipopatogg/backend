using Pactogg.Api.Common;
using Pactogg.Api.Common.Extensions;
using Pactogg.Api.Modules.Groups.Dtos;

namespace Pactogg.Api.Modules.Groups;

public static class GroupEndpoints
{
    public static IEndpointRouteBuilder MapGroupEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/groups").WithTags("Groups");

        group.MapPost("", async (
                CreateGroupRequest request,
                GroupService service,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var response = await service.CreateAsync(context.User.GetRequiredObjectId(), request, cancellationToken);
                return ApiResults.Created(response);
            })
            .RequireAuthorization()
            .AddEndpointFilter<ValidationFilter<CreateGroupRequest>>()
            .WithName("CreateGroup")
            .Produces<GroupResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}
