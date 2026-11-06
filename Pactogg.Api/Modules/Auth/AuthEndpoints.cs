using Pactogg.Api.Common.Extensions;
using Pactogg.Api.Common;
using Pactogg.Api.Modules.Auth.Dtos;

namespace Pactogg.Api.Modules.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth").WithTags("Auth");

        group.MapPost("/register", async (
                RegisterRequest request,
                AuthService service,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var session = await service.RegisterAsync(request, context.Connection.RemoteIpAddress?.ToString(), cancellationToken);
                return ApiResults.Created(session);
            })
            .AllowAnonymous()
            .AddEndpointFilter<ValidationFilter<RegisterRequest>>()
            .WithName("Register")
            .Produces<SessionResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/login", async (
                LoginRequest request,
                AuthService service,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var session = await service.LoginAsync(request, context.Connection.RemoteIpAddress?.ToString(), cancellationToken);
                return ApiResults.Ok(session);
            })
            .AllowAnonymous()
            .AddEndpointFilter<ValidationFilter<LoginRequest>>()
            .WithName("Login")
            .Produces<SessionResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("/google", async (
                GoogleAuthRequest request,
                AuthService service,
                CancellationToken cancellationToken) =>
            {
                var (session, created) = await service.GoogleAsync(request, cancellationToken);
                return created ? ApiResults.Created(session) : ApiResults.Ok(session);
            })
            .AllowAnonymous()
            .AddEndpointFilter<ValidationFilter<GoogleAuthRequest>>()
            .WithName("GoogleAuth")
            .Produces<SessionResponse>()
            .Produces<SessionResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        group.MapPost("/refresh", async (
                RefreshRequest request,
                AuthService service,
                CancellationToken cancellationToken) =>
            {
                var session = await service.RefreshAsync(request, cancellationToken);
                return ApiResults.Ok(session);
            })
            .AllowAnonymous()
            .AddEndpointFilter<ValidationFilter<RefreshRequest>>()
            .WithName("RefreshToken")
            .Produces<SessionResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/logout", async (
                HttpContext context,
                AuthService service,
                CancellationToken cancellationToken) =>
            {
                LogoutRequest? request = null;
                if (context.Request.ContentLength is > 0)
                {
                    try
                    {
                        request = await context.Request.ReadFromJsonAsync<LogoutRequest>(cancellationToken: cancellationToken);
                    }
                    catch (BadHttpRequestException)
                    {
                        throw ApiException.BadRequest(ErrorCodes.InvalidInput, "Cuerpo JSON inválido.");
                    }
                    catch (System.Text.Json.JsonException)
                    {
                        throw ApiException.BadRequest(ErrorCodes.InvalidInput, "Cuerpo JSON inválido.");
                    }
                }

                await service.LogoutAsync(context.User, request, cancellationToken);
                return Results.NoContent();
            })
            .RequireAuthorization()
            .WithName("Logout")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        // ---- AUTH-05: verificación de correo ----
        group.MapPost("/verify-email", async (TokenRequest request, AccountService service, CancellationToken cancellationToken) =>
                ApiResults.Ok(await service.VerifyEmailAsync(request, cancellationToken)))
            .AllowAnonymous()
            .AddEndpointFilter<ValidationFilter<TokenRequest>>()
            .WithName("VerifyEmail")
            .Produces<VerifyEmailResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/verify-email/resend", async (AccountService service, HttpContext context, CancellationToken cancellationToken) =>
            {
                await service.ResendVerificationAsync(context.User.GetRequiredObjectId(), cancellationToken);
                return Results.Accepted();
            })
            .RequireAuthorization()
            .WithName("ResendVerificationEmail")
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        // ---- AUTH-06 / AUTH-07: recuperación de contraseña ----
        group.MapPost("/forgot-password", async (ForgotPasswordRequest request, AccountService service, CancellationToken cancellationToken) =>
            {
                await service.ForgotPasswordAsync(request, cancellationToken);
                return Results.Accepted();
            })
            .AllowAnonymous()
            .AddEndpointFilter<ValidationFilter<ForgotPasswordRequest>>()
            .WithName("ForgotPassword")
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("/reset-password", async (ResetPasswordRequest request, AccountService service, CancellationToken cancellationToken) =>
            {
                await service.ResetPasswordAsync(request, cancellationToken);
                return ApiResults.Ok(new { password_reset = true });
            })
            .AllowAnonymous()
            .AddEndpointFilter<ValidationFilter<ResetPasswordRequest>>()
            .WithName("ResetPassword")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }
}
