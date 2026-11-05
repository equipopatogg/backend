using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Pactogg.Api.Common;
using Pactogg.Api.Common.Middleware;
using Pactogg.Api.Infrastructure.Options;
using Pactogg.Api.Infrastructure.Redis;

namespace Pactogg.Api.Common.Middleware;

public sealed class RedisRateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IRateLimitStore _store;
    private readonly RateLimitOptions _options;

    public RedisRateLimitMiddleware(RequestDelegate next, IRateLimitStore store, IOptions<RateLimitOptions> options)
    {
        _next = next;
        _store = store;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_options.Enabled || !context.Request.Path.StartsWithSegments("/api"))
        {
            await _next(context);
            return;
        }

        var policy = ResolvePolicy(context.Request.Path);
        var identity = policy.Name is "login" or "register" or "nickname"
            ? context.Connection.RemoteIpAddress?.ToString() ?? "unknown"
            : context.User.FindFirstValue("sub")
                ?? context.Connection.RemoteIpAddress?.ToString()
                ?? "unknown";

        var key = $"{policy.Name}:{identity}";
        var count = await _store.IncrementAsync(key, policy.Window, context.RequestAborted);
        if (count > policy.Limit)
        {
            context.Response.Headers["Retry-After"] = Math.Ceiling(policy.Window.TotalSeconds).ToString("0");
            await ErrorResponseWriter.WriteAsync(
                context,
                StatusCodes.Status429TooManyRequests,
                ErrorCodes.RateLimited,
                "Demasiadas solicitudes. Intenta más tarde.",
                cancellationToken: context.RequestAborted);
            return;
        }

        await _next(context);
    }

    private (string Name, int Limit, TimeSpan Window) ResolvePolicy(PathString path)
    {
        if (string.Equals(path.Value, "/api/v1/auth/register", StringComparison.OrdinalIgnoreCase))
        {
            return ("register", _options.RegisterPermitLimit, TimeSpan.FromSeconds(_options.RegisterWindowSeconds));
        }

        if (string.Equals(path.Value, "/api/v1/auth/login", StringComparison.OrdinalIgnoreCase)
            || string.Equals(path.Value, "/api/v1/auth/refresh", StringComparison.OrdinalIgnoreCase)
            || string.Equals(path.Value, "/api/v1/auth/google", StringComparison.OrdinalIgnoreCase))
        {
            return ("login", _options.LoginPermitLimit, TimeSpan.FromSeconds(_options.LoginWindowSeconds));
        }

        if (string.Equals(path.Value, "/api/v1/users/check-nickname", StringComparison.OrdinalIgnoreCase))
        {
            return ("nickname", _options.NicknameCheckPermitLimit, TimeSpan.FromSeconds(_options.NicknameCheckWindowSeconds));
        }

        if (string.Equals(path.Value, "/api/v1/groups/join", StringComparison.OrdinalIgnoreCase))
        {
            return ("join", _options.JoinPermitLimit, TimeSpan.FromSeconds(_options.JoinWindowSeconds));
        }

        if (path.Value?.EndsWith("/evidence", StringComparison.OrdinalIgnoreCase) == true)
        {
            return ("evidence", _options.EvidencePermitLimit, TimeSpan.FromSeconds(_options.EvidenceWindowSeconds));
        }

        return ("default", _options.DefaultPermitLimit, TimeSpan.FromSeconds(_options.DefaultWindowSeconds));
    }
}
