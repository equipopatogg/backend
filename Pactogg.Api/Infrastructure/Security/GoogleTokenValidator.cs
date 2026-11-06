using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Pactogg.Api.Common;
using Pactogg.Api.Infrastructure.Options;

namespace Pactogg.Api.Infrastructure.Security;

/// <summary>Identidad de Google ya validada: firma, emisor, audiencia, vigencia y correo verificado por Google.</summary>
public sealed record GoogleIdentity(string Subject, string Email, string? Name);

public interface IGoogleTokenValidator
{
    /// <summary>Valida el ID token de Google (firma, emisor, audiencia, vigencia y email_verified). Null si algo no cumple.</summary>
    Task<GoogleIdentity?> ValidateAsync(string idToken, CancellationToken cancellationToken);
}

/// <summary>
/// Verifica el ID token con las llaves públicas de Google (descubrimiento OpenID, en caché).
/// Falla cerrada: sin Client IDs configurados responde 503 y nunca acepta tokens.
/// </summary>
public sealed class GoogleTokenValidator : IGoogleTokenValidator
{
    private static readonly string[] Issuers = ["accounts.google.com", "https://accounts.google.com"];
    private readonly ConfigurationManager<OpenIdConnectConfiguration> _configuration;
    private readonly IReadOnlyList<string> _clientIds;
    private readonly ILogger<GoogleTokenValidator> _logger;
    private readonly JsonWebTokenHandler _handler = new();

    public GoogleTokenValidator(IOptions<AuthOptions> options, ILogger<GoogleTokenValidator> logger)
    {
        _clientIds = options.Value.GoogleClientIds.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        _logger = logger;
        _configuration = new ConfigurationManager<OpenIdConnectConfiguration>(
            "https://accounts.google.com/.well-known/openid-configuration",
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever { RequireHttps = true });
    }

    public async Task<GoogleIdentity?> ValidateAsync(string idToken, CancellationToken cancellationToken)
    {
        if (_clientIds.Count == 0)
        {
            throw ApiException.ServiceUnavailable(ErrorCodes.GoogleAuthUnavailable, "El inicio de sesión con Google no está configurado.");
        }

        OpenIdConnectConfiguration configuration;
        try
        {
            configuration = await _configuration.GetConfigurationAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "No se pudieron obtener las llaves de Google.");
            throw ApiException.ServiceUnavailable(ErrorCodes.GoogleAuthUnavailable, "Google no respondió; intenta de nuevo.");
        }

        var result = await _handler.ValidateTokenAsync(idToken, new TokenValidationParameters
        {
            ValidIssuers = Issuers,
            ValidAudiences = _clientIds,
            IssuerSigningKeys = configuration.SigningKeys,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2)
        });

        if (!result.IsValid)
        {
            return null;
        }

        var claims = result.Claims;
        var subject = claims.TryGetValue("sub", out var sub) ? sub?.ToString() : null;
        var email = claims.TryGetValue("email", out var mail) ? mail?.ToString() : null;
        var verified = claims.TryGetValue("email_verified", out var flag)
            && (flag is true || string.Equals(flag?.ToString(), "true", StringComparison.OrdinalIgnoreCase));
        var name = claims.TryGetValue("name", out var display) ? display?.ToString() : null;

        // Un correo que Google no verificó se trata igual que un token inválido.
        return !verified || string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(email)
            ? null
            : new GoogleIdentity(subject, email.Trim().ToLowerInvariant(), name);
    }
}
