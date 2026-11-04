using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using Pactogg.Api.Common;
using Pactogg.Api.Common.Extensions;
using Pactogg.Api.Infrastructure.Audit;
using Pactogg.Api.Infrastructure.Options;
using Pactogg.Api.Infrastructure.Security;
using Pactogg.Api.Modules.Auth.Abstractions;
using Pactogg.Api.Modules.Auth.Domain;
using Pactogg.Api.Modules.Auth.Dtos;

namespace Pactogg.Api.Modules.Auth;

public sealed class AuthService
{
    private readonly IUserRepository _users;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _tokens;
    private readonly ILoginAttemptStore _loginAttempts;
    private readonly ITokenRevocationStore _revocations;
    private readonly IAuditService _audit;
    private readonly AuthOptions _authOptions;
    private readonly JwtOptions _jwtOptions;

    public AuthService(
        IUserRepository users,
        IRefreshTokenRepository refreshTokens,
        IPasswordHasher passwordHasher,
        IJwtTokenService tokens,
        ILoginAttemptStore loginAttempts,
        ITokenRevocationStore revocations,
        IAuditService audit,
        IOptions<AuthOptions> authOptions,
        IOptions<JwtOptions> jwtOptions)
    {
        _users = users;
        _refreshTokens = refreshTokens;
        _passwordHasher = passwordHasher;
        _tokens = tokens;
        _loginAttempts = loginAttempts;
        _revocations = revocations;
        _audit = audit;
        _authOptions = authOptions.Value;
        _jwtOptions = jwtOptions.Value;
    }

    public async Task<SessionResponse> RegisterAsync(RegisterRequest request, string? ip, CancellationToken cancellationToken)
    {
        if (!request.AcceptPrivacy)
        {
            throw ApiException.Unprocessable(ErrorCodes.ValidationError, "Debes aceptar la política de privacidad.");
        }

        if (!string.Equals(request.PrivacyVersion, _authOptions.CurrentPrivacyVersion, StringComparison.Ordinal))
        {
            throw ApiException.Unprocessable(ErrorCodes.ValidationError, "La versión de privacidad no es la vigente.");
        }

        var email = request.Email.Trim().ToLowerInvariant();
        var nickname = request.Nickname.Trim();
        var nicknameLower = nickname.ToLowerInvariant();

        if (await _users.GetByEmailAsync(email, cancellationToken) is not null)
        {
            throw ApiException.Conflict(ErrorCodes.EmailAlreadyExists, "Ya existe una cuenta con ese correo.");
        }

        if (await _users.GetByNicknameAsync(nicknameLower, cancellationToken) is not null)
        {
            throw ApiException.Conflict(ErrorCodes.NicknameAlreadyExists, "El apodo ya está en uso.");
        }

        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = ObjectId.GenerateNewId(),
            Email = email,
            Nickname = nickname,
            NicknameLower = nicknameLower,
            PasswordHash = _passwordHasher.Hash(request.Password),
            EmailVerified = false,
            Stats = new UserStats(),
            Consent = new ConsentRecord
            {
                Version = _authOptions.CurrentPrivacyVersion,
                AcceptedAt = now
            },
            Status = "active",
            CreatedAt = now,
            UpdatedAt = now
        };

        await _users.CreateAsync(user, cancellationToken);
        var session = await CreateSessionAsync(user, request.DeviceId, cancellationToken);
        await _audit.AppendAsync("user.registered", user.Id, "user", user.Id, null, new { user_id = user.Id.ToString() }, cancellationToken);
        return session;
    }

    public async Task<SessionResponse> LoginAsync(LoginRequest request, string? ip, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var attemptKey = $"{email}|{ip ?? "unknown"}";
        var failures = await _loginAttempts.GetFailuresAsync(attemptKey, cancellationToken);
        if (failures >= _authOptions.MaxFailedAttempts)
        {
            throw ApiException.TooManyRequests("Demasiados intentos. Espera antes de volver a intentar.");
        }

        var user = await _users.GetByEmailAsync(email, cancellationToken);
        if (user is null || !_passwordHasher.Verify(user.PasswordHash, request.Password))
        {
            await _loginAttempts.RecordFailureAsync(attemptKey, TimeSpan.FromMinutes(_authOptions.LockoutMinutes), cancellationToken);
            throw ApiException.Unauthorized(ErrorCodes.InvalidCredentials, "Correo o contraseña incorrectos.");
        }

        if (user.Status != "active")
        {
            throw ApiException.Unauthorized(ErrorCodes.Unauthorized, "La cuenta no está activa.");
        }

        await _loginAttempts.ClearAsync(attemptKey, cancellationToken);
        await _users.UpdateLastLoginAsync(user.Id, DateTime.UtcNow, cancellationToken);
        var session = await CreateSessionAsync(user, request.DeviceId, cancellationToken);
        await _audit.AppendAsync("auth.login", user.Id, "user", user.Id, null, new { user_id = user.Id.ToString() }, cancellationToken);
        return session;
    }

    public async Task<SessionResponse> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken)
    {
        var hash = _tokens.HashRefreshToken(request.RefreshToken);
        var stored = await _refreshTokens.GetByHashAsync(hash, cancellationToken);
        if (stored is null || stored.RevokedAt is not null || stored.ExpiresAt <= DateTime.UtcNow)
        {
            throw ApiException.Unauthorized();
        }

        var user = await _users.GetByIdAsync(stored.UserId, cancellationToken);
        if (user is null || user.Status != "active")
        {
            throw ApiException.Unauthorized();
        }

        await _refreshTokens.RevokeAsync(stored.Id, DateTime.UtcNow, cancellationToken);
        var session = await CreateSessionAsync(user, request.DeviceId ?? stored.DeviceId, cancellationToken);
        await _audit.AppendAsync("auth.refresh", user.Id, "refresh_token", stored.Id, null, new { user_id = user.Id.ToString() }, cancellationToken);
        return session;
    }

    public async Task LogoutAsync(
        ClaimsPrincipal principal,
        LogoutRequest? request,
        CancellationToken cancellationToken)
    {
        var userId = principal.GetRequiredObjectId();
        var jti = principal.FindFirstValue("jti");
        var exp = principal.FindFirstValue("exp");
        if (!string.IsNullOrWhiteSpace(jti) && long.TryParse(exp, out var expUnix))
        {
            await _revocations.RevokeAsync(jti, DateTimeOffset.FromUnixTimeSeconds(expUnix).UtcDateTime, cancellationToken);
        }

        var now = DateTime.UtcNow;
        if (request?.All == true)
        {
            await _refreshTokens.RevokeAllForUserAsync(userId, now, cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(request?.RefreshToken))
        {
            var stored = await _refreshTokens.GetByHashAsync(_tokens.HashRefreshToken(request.RefreshToken), cancellationToken);
            if (stored is not null && stored.UserId == userId)
            {
                await _refreshTokens.RevokeAsync(stored.Id, now, cancellationToken);
            }
        }

        await _audit.AppendAsync("auth.logout", userId, "user", userId, null, new { all = request?.All == true }, cancellationToken);
    }

    private async Task<SessionResponse> CreateSessionAsync(User user, string? deviceId, CancellationToken cancellationToken)
    {
        var access = _tokens.CreateAccessToken(user);
        var refresh = _tokens.CreateRefreshToken();
        var refreshExpiresAt = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenDays);
        await _refreshTokens.CreateAsync(new RefreshToken
        {
            Id = ObjectId.GenerateNewId(),
            UserId = user.Id,
            TokenHash = _tokens.HashRefreshToken(refresh),
            DeviceId = deviceId,
            ExpiresAt = refreshExpiresAt,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        return new SessionResponse
        {
            User = new UserResponse
            {
                Id = user.Id.ToString(),
                Email = user.Email,
                Nickname = user.Nickname,
                EmailVerified = user.EmailVerified,
                CreatedAt = user.CreatedAt
            },
            AccessToken = access.Token,
            RefreshToken = refresh,
            ExpiresIn = Math.Max(1, (int)(access.ExpiresAt - DateTime.UtcNow).TotalSeconds)
        };
    }
}
