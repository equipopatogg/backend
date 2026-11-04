using Pactogg.Api.Common;
using Pactogg.Api.Common.Extensions;
using Pactogg.Api.Modules.Users.Dtos;

namespace Pactogg.Api.Modules.Users;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/users").WithTags("Users");

        group.MapGet("/me", async (UserService service, HttpContext context, CancellationToken cancellationToken) =>
                ApiResults.Ok(await service.GetMeAsync(context.User.GetRequiredObjectId(), cancellationToken)))
            .RequireAuthorization()
            .WithName("GetMyProfile")
            .Produces<ProfileResponse>();

        group.MapPatch("/me", async (
                UpdateProfileRequest request,
                UserService service,
                HttpContext context,
                CancellationToken cancellationToken) =>
                ApiResults.Ok(await service.UpdateMeAsync(context.User.GetRequiredObjectId(), request, cancellationToken)))
            .RequireAuthorization()
            .AddEndpointFilter<ValidationFilter<UpdateProfileRequest>>()
            .WithName("UpdateMyProfile")
            .Produces<ProfileResponse>()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapGet("/check-nickname", async (string? nickname, UserService service, CancellationToken cancellationToken) =>
                ApiResults.Ok(await service.CheckNicknameAsync(nickname, cancellationToken)))
            .AllowAnonymous()
            .WithName("CheckNickname")
            .Produces<NicknameAvailabilityResponse>()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        return app;
    }
}
