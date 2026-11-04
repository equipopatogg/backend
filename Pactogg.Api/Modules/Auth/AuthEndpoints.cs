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

        return app;
    }
}
