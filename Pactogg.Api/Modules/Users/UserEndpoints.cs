using Pactogg.Api.Modules.Auth.Dtos;
using Pactogg.Api.Modules.Auth;
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

        group.MapDelete("/me", async ([Microsoft.AspNetCore.Mvc.FromBody] DeleteAccountRequest request, AccountDeletionService service, HttpContext context, CancellationToken cancellationToken) =>
            {
                await service.DeleteAsync(context.User, request, cancellationToken);
                return Results.NoContent();
            })
            .RequireAuthorization()
            .AddEndpointFilter<ValidationFilter<DeleteAccountRequest>>()
            .WithName("DeleteAccount")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapGet("/check-nickname", async (string? nickname, UserService service, CancellationToken cancellationToken) =>
                ApiResults.Ok(await service.CheckNicknameAsync(nickname, cancellationToken)))
            .AllowAnonymous()
            .WithName("CheckNickname")
            .Produces<NicknameAvailabilityResponse>()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapGet("/{id}", async (string id, UserService service, HttpContext context, CancellationToken cancellationToken) =>
                ApiResults.Ok(await service.GetPublicAsync(context.User.GetRequiredObjectId(), id, cancellationToken)))
            .RequireAuthorization()
            .WithName("GetPublicProfile")
            .Produces<PublicProfileResponse>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/me/password", async (
                ChangePasswordRequest request,
                AccountService account,
                AuthService auth,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var user = await account.ChangePasswordAsync(context.User.GetRequiredObjectId(), request, cancellationToken);
                return ApiResults.Ok(await auth.IssueSessionAsync(user, request.DeviceId, cancellationToken));
            })
            .RequireAuthorization()
            .AddEndpointFilter<ValidationFilter<ChangePasswordRequest>>()
            .WithName("ChangePassword")
            .Produces<SessionResponse>()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/me/consent", async (ConsentRequest request, AccountService account, HttpContext context, CancellationToken cancellationToken) =>
                ApiResults.Ok(await account.AcceptConsentAsync(context.User.GetRequiredObjectId(), request, cancellationToken)))
            .RequireAuthorization()
            .AddEndpointFilter<ValidationFilter<ConsentRequest>>()
            .WithName("AcceptConsent")
            .Produces<ConsentResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return app;
    }
}
