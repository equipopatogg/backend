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

        group.MapGet("", async (
                string? q,
                int? limit,
                string? cursor,
                GroupService service,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var (items, meta) = await service.ListAsync(context.User.GetRequiredObjectId(), q, limit ?? 20, cursor, cancellationToken);
                return ApiResults.Ok(items, meta);
            })
            .RequireAuthorization()
            .WithName("ListMyGroups")
            .Produces<IReadOnlyList<GroupResponse>>()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/join", async (
                JoinGroupRequest request,
                GroupService service,
                HttpContext context,
                CancellationToken cancellationToken) =>
                ApiResults.Ok(await service.JoinAsync(context.User.GetRequiredObjectId(), request, cancellationToken)))
            .RequireAuthorization()
            .AddEndpointFilter<ValidationFilter<JoinGroupRequest>>()
            .WithName("JoinGroup")
            .Produces<GroupResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapGet("/{id}", async (
                string id,
                GroupService service,
                HttpContext context,
                CancellationToken cancellationToken) =>
                ApiResults.Ok(await service.GetAsync(context.User.GetRequiredObjectId(), id, cancellationToken)))
            .RequireAuthorization()
            .WithName("GetGroup")
            .Produces<GroupResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id}/members", async (
                string id,
                string? q,
                int? limit,
                string? cursor,
                GroupService service,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var (items, meta) = await service.ListMembersAsync(context.User.GetRequiredObjectId(), id, q, limit ?? 20, cursor, cancellationToken);
                return ApiResults.Ok(items, meta);
            })
            .RequireAuthorization()
            .WithName("ListGroupMembers")
            .Produces<IReadOnlyList<GroupMemberResponse>>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // ---------- GRP-04..07, MEM-03..06: administración ----------
        group.MapPatch("/{id}", async (string id, UpdateGroupRequest request, GroupService service, HttpContext context, CancellationToken cancellationToken) =>
                ApiResults.Ok(await service.UpdateAsync(context.User.GetRequiredObjectId(), id, request, cancellationToken)))
            .RequireAuthorization()
            .AddEndpointFilter<ValidationFilter<UpdateGroupRequest>>()
            .WithName("UpdateGroup")
            .Produces<GroupResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapDelete("/{id}", async (string id, GroupService service, HttpContext context, CancellationToken cancellationToken) =>
            {
                await service.DeleteAsync(context.User.GetRequiredObjectId(), id, cancellationToken);
                return Results.NoContent();
            })
            .RequireAuthorization()
            .WithName("DeleteGroup")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/{id}/invite-code/regenerate", async (string id, GroupService service, HttpContext context, CancellationToken cancellationToken) =>
                ApiResults.Ok(await service.RegenerateInviteAsync(context.User.GetRequiredObjectId(), id, cancellationToken)))
            .RequireAuthorization()
            .WithName("RegenerateInviteCode")
            .Produces<GroupResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPatch("/{id}/invite-code", async (string id, SetInviteActiveRequest request, GroupService service, HttpContext context, CancellationToken cancellationToken) =>
                ApiResults.Ok(await service.SetInviteActiveAsync(context.User.GetRequiredObjectId(), id, request, cancellationToken)))
            .RequireAuthorization()
            .AddEndpointFilter<ValidationFilter<SetInviteActiveRequest>>()
            .WithName("SetInviteCodeActive")
            .Produces<GroupResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/{id}/members/me", async (string id, GroupService service, HttpContext context, CancellationToken cancellationToken) =>
                ApiResults.Ok(await service.GetMyMembershipAsync(context.User.GetRequiredObjectId(), id, cancellationToken)))
            .RequireAuthorization()
            .WithName("GetMyMembership")
            .Produces<GroupMemberResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapDelete("/{id}/members/me", async (string id, GroupService service, HttpContext context, CancellationToken cancellationToken) =>
            {
                await service.LeaveAsync(context.User.GetRequiredObjectId(), id, cancellationToken);
                return Results.NoContent();
            })
            .RequireAuthorization()
            .WithName("LeaveGroup")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPatch("/{id}/members/{uid}", async (string id, string uid, SetRoleRequest request, GroupService service, HttpContext context, CancellationToken cancellationToken) =>
                ApiResults.Ok(await service.SetRoleAsync(context.User.GetRequiredObjectId(), id, uid, request, cancellationToken)))
            .RequireAuthorization()
            .AddEndpointFilter<ValidationFilter<SetRoleRequest>>()
            .WithName("SetMemberRole")
            .Produces<GroupMemberResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{id}/members/{uid}", async (string id, string uid, GroupService service, HttpContext context, CancellationToken cancellationToken) =>
            {
                await service.RemoveMemberAsync(context.User.GetRequiredObjectId(), id, uid, cancellationToken);
                return Results.NoContent();
            })
            .RequireAuthorization()
            .WithName("RemoveMember")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }
}
