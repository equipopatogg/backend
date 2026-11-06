using System.Net;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using Pactogg.Api.Common;
using Pactogg.Api.Infrastructure.Audit;
using Pactogg.Api.Infrastructure.Email;
using Pactogg.Api.Infrastructure.Options;
using Pactogg.Api.Infrastructure.Redis;
using Pactogg.Api.Infrastructure.Security;
using Pactogg.Api.Modules.Auth.Abstractions;
using Pactogg.Api.Modules.Auth.Domain;
using Pactogg.Api.Modules.Auth.Dtos;

namespace Pactogg.Api.Modules.Auth;

/// <summary>
/// AUTH-05 verificación de correo, AUTH-06/07 recuperación de contraseña y cambio de contraseña (CLARIFICACION #5, #11, #13).
/// Las respuestas de recuperación son iguales exista o no la cuenta.
/// </summary>
public sealed class AccountService
{
    public const string VerifyPurpose = "verify_email";
    public const string ResetPurpose = "reset_password";

    private readonly IUserRepository _users;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IPasswordHasher _passwordHasher;
    private readonly OneTimeTokenStore _oneTimeTokens;
    private readonly IEmailSender _email;
    private readonly IRateLimitStore _limits;
    private readonly IAuditService _audit;
    private readonly ICacheService _cache;
    private readonly EmailOptions _emailOptions;
    private readonly AuthOptions _authOptions;

    public AccountService(
        IUserRepository users,
        IRefreshTokenRepository refreshTokens,
        IPasswordHasher passwordHasher,
        OneTimeTokenStore oneTimeTokens,
        IEmailSender email,
        IRateLimitStore limits,
        IAuditService audit,
        ICacheService cache,
        IOptions<EmailOptions> emailOptions,
        IOptions<AuthOptions> authOptions)
    {
        _users = users;
        _refreshTokens = refreshTokens;
        _passwordHasher = passwordHasher;
        _oneTimeTokens = oneTimeTokens;
        _email = email;
        _limits = limits;
        _audit = audit;
        _cache = cache;
        _emailOptions = emailOptions.Value;
        _authOptions = authOptions.Value;
    }

    /// <summary>Envía el correo de verificación. Se llama al registrarse y en AUTH-05 reenviar.</summary>
    public async Task SendVerificationAsync(User user, CancellationToken cancellationToken)
    {
        if (user.EmailVerified)
        {
            return;
        }

        var token = await _oneTimeTokens.IssueAsync(VerifyPurpose, user.Id, TimeSpan.FromHours(_emailOptions.VerifyTokenHours), cancellationToken);
        var link = $"{_emailOptions.VerifyEmailLink}?token={Uri.EscapeDataString(token)}";
        await _email.SendAsync(
            BuildMessage(
                user.Email,
                "Verifica tu correo en PactoGG",
                $"Hola {user.Nickname}, confirma tu correo para crear o unirte a grupos.",
                "Verificar correo",
                link,
                token,
                $"El enlace caduca en {_emailOptions.VerifyTokenHours} horas."),
            cancellationToken);
    }

    public async Task ResendVerificationAsync(ObjectId userId, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken) ?? throw ApiException.Unauthorized();
        if (user.EmailVerified)
        {
            throw ApiException.Conflict(ErrorCodes.EmailAlreadyVerified, "Tu correo ya está verificado.");
        }

        if (await _limits.IncrementAsync($"verify-resend:{userId}", TimeSpan.FromHours(1), cancellationToken) > 3)
        {
            throw ApiException.TooManyRequests("Ya enviamos varios correos. Revisa tu bandeja o intenta en una hora.");
        }

        await SendVerificationAsync(user, cancellationToken);
    }

    public async Task<VerifyEmailResponse> VerifyEmailAsync(TokenRequest request, CancellationToken cancellationToken)
    {
        var userId = await _oneTimeTokens.ConsumeAsync(VerifyPurpose, request.Token, cancellationToken)
            ?? throw ApiException.BadRequest(ErrorCodes.InvalidOrExpiredToken, "El enlace no es válido o ya caducó. Pide uno nuevo.");
        var now = DateTime.UtcNow;
        await _users.SetEmailVerifiedAsync(userId, now, cancellationToken);
        await _cache.BumpVersionAsync(CacheExtensions.UserScope(userId), cancellationToken);
        await _audit.AppendAsync("user.email_verified", userId, "user", userId, null, new { user_id = userId.ToString() }, cancellationToken);
        return new VerifyEmailResponse { EmailVerified = true };
    }

    /// <summary>AUTH-06: siempre 202, exista o no la cuenta (evita enumerar correos).</summary>
    public async Task ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await _limits.IncrementAsync($"forgot:{email}", TimeSpan.FromHours(1), cancellationToken) > 3)
        {
            // Sin error visible: la respuesta sigue siendo la misma.
            return;
        }

        var user = await _users.GetByEmailAsync(email, cancellationToken);
        if (user is null || user.Status != "active")
        {
            return;
        }

        var token = await _oneTimeTokens.IssueAsync(ResetPurpose, user.Id, TimeSpan.FromMinutes(_emailOptions.ResetTokenMinutes), cancellationToken);
        var link = $"{_emailOptions.ResetPasswordLink}?token={Uri.EscapeDataString(token)}";
        await _email.SendAsync(
            BuildMessage(
                user.Email,
                "Restablece tu contraseña de PactoGG",
                $"Hola {user.Nickname}, alguien pidió cambiar la contraseña de tu cuenta. Si no fuiste tú, ignora este correo.",
                "Cambiar contraseña",
                link,
                token,
                $"El enlace sirve una sola vez y caduca en {_emailOptions.ResetTokenMinutes} minutos."),
            cancellationToken);
        await _audit.AppendAsync("auth.password_reset_requested", user.Id, "user", user.Id, null, new { user_id = user.Id.ToString() }, cancellationToken);
    }

    /// <summary>AUTH-07: cambia la contraseña con el token y revoca todas las sesiones.</summary>
    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var userId = await _oneTimeTokens.ConsumeAsync(ResetPurpose, request.Token, cancellationToken)
            ?? throw ApiException.BadRequest(ErrorCodes.InvalidOrExpiredToken, "El enlace no es válido o ya caducó. Pide uno nuevo.");
        var now = DateTime.UtcNow;
        await _users.UpdatePasswordAsync(userId, _passwordHasher.Hash(request.NewPassword), now, cancellationToken);

        // Quien abrió el enlace demostró que controla el correo.
        await _users.SetEmailVerifiedAsync(userId, now, cancellationToken);
        await _refreshTokens.RevokeAllForUserAsync(userId, now, cancellationToken);
        await _cache.BumpVersionAsync(CacheExtensions.UserScope(userId), cancellationToken);
        await _audit.AppendAsync("auth.password_reset", userId, "user", userId, null, new { user_id = userId.ToString() }, cancellationToken);
    }

    /// <summary>Cambio de contraseña con la actual. Revoca las demás sesiones; la app recibe una sesión nueva.</summary>
    public async Task<User> ChangePasswordAsync(ObjectId userId, ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken) ?? throw ApiException.Unauthorized();
        if (string.IsNullOrEmpty(user.PasswordHash))
        {
            // Cuenta creada con Google: puede fijar contraseña con "olvidé mi contraseña".
            throw ApiException.Conflict(ErrorCodes.PasswordNotSet, "Tu cuenta entra con Google. Usa \"Olvidé mi contraseña\" para crear una.");
        }

        if (await _limits.IncrementAsync($"change-password:{userId}", TimeSpan.FromMinutes(15), cancellationToken) > _authOptions.MaxFailedAttempts)
        {
            throw ApiException.TooManyRequests("Demasiados intentos. Espera antes de volver a intentar.");
        }

        if (!_passwordHasher.Verify(user.PasswordHash, request.CurrentPassword))
        {
            throw ApiException.Unprocessable(ErrorCodes.InvalidCredentials, "La contraseña actual no es correcta.");
        }

        if (string.Equals(request.CurrentPassword, request.NewPassword, StringComparison.Ordinal))
        {
            throw ApiException.Unprocessable(ErrorCodes.ValidationError, "La contraseña nueva debe ser distinta de la actual.");
        }

        var now = DateTime.UtcNow;
        await _users.UpdatePasswordAsync(userId, _passwordHasher.Hash(request.NewPassword), now, cancellationToken);
        await _refreshTokens.RevokeAllForUserAsync(userId, now, cancellationToken);
        await _audit.AppendAsync("auth.password_changed", userId, "user", userId, null, new { user_id = userId.ToString() }, cancellationToken);
        return user;
    }

    /// <summary>USR-07: registra la aceptación de la versión vigente del aviso de privacidad.</summary>
    public async Task<ConsentResponse> AcceptConsentAsync(ObjectId userId, ConsentRequest request, CancellationToken cancellationToken)
    {
        if (!string.Equals(request.Version, _authOptions.CurrentPrivacyVersion, StringComparison.Ordinal))
        {
            throw ApiException.BadRequest(ErrorCodes.ValidationError, $"La versión vigente del aviso es {_authOptions.CurrentPrivacyVersion}.");
        }

        var now = DateTime.UtcNow;
        await _users.UpdateConsentAsync(userId, request.Version, now, cancellationToken);
        await _cache.BumpVersionAsync(CacheExtensions.UserScope(userId), cancellationToken);
        await _audit.AppendAsync("user.consent", userId, "user", userId, null, new { version = request.Version }, cancellationToken);
        return new ConsentResponse { Version = request.Version, AcceptedAt = now };
    }

    private static EmailMessage BuildMessage(string to, string subject, string intro, string button, string link, string token, string footer)
    {
        var safeIntro = WebUtility.HtmlEncode(intro);
        var safeLink = WebUtility.HtmlEncode(link);
        var safeToken = WebUtility.HtmlEncode(token);
        var html = $"""
            <div style="font-family:Arial,sans-serif;max-width:480px;margin:auto;color:#1b1b1f">
              <h2 style="color:#6c2bd9">PactoGG</h2>
              <p>{safeIntro}</p>
              <p><a href="{safeLink}" style="display:inline-block;background:#6c2bd9;color:#fff;padding:12px 20px;border-radius:8px;text-decoration:none">{WebUtility.HtmlEncode(button)}</a></p>
              <p style="font-size:13px;color:#555">Si el botón no abre la app, copia este código en PactoGG:</p>
              <p style="font-family:monospace;font-size:13px;word-break:break-all;background:#f3f0fa;padding:8px;border-radius:6px">{safeToken}</p>
              <p style="font-size:12px;color:#777">{WebUtility.HtmlEncode(footer)}</p>
            </div>
            """;
        var text = $"{intro}\n\n{button}: {link}\n\nCódigo: {token}\n\n{footer}";
        return new EmailMessage(to, subject, text, html);
    }
}
