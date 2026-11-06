namespace Pactogg.Api.Infrastructure.Email;

/// <summary>
/// Envío de correos (verificación y recuperación, AUTH-05/06). Por SMTP: Amazon SES (CLARIFICACION #5)
/// o el correo único del proyecto. Sin host configurado no se envía nada y solo se registra el intento.
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    public string? SmtpUser { get; set; }

    /// <summary>Secreto: viene de Parameter Store (/pactogg/email/smtp-password), nunca del repositorio.</summary>
    public string? SmtpPassword { get; set; }

    public string FromAddress { get; set; } = "no-reply@pactogg.app";
    public string FromName { get; set; } = "PactoGG";

    /// <summary>Base del enlace que abre la app (deep link). Se agrega ?token=...</summary>
    public string VerifyEmailLink { get; set; } = "pactogg://auth/verify-email";
    public string ResetPasswordLink { get; set; } = "pactogg://auth/reset-password";

    public int VerifyTokenHours { get; set; } = 24;
    public int ResetTokenMinutes { get; set; } = 60;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(SmtpHost);
}
