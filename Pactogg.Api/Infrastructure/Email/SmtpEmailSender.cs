using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace Pactogg.Api.Infrastructure.Email;

/// <summary>SMTP con STARTTLS (SES: email-smtp.us-east-1.amazonaws.com:587). Nunca registra el destinatario ni el cuerpo.</summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        if (!_options.IsConfigured)
        {
            _logger.LogWarning("Correo no configurado (Email:SmtpHost vacío); no se envió el mensaje \"{Subject}\".", message.Subject);
            return false;
        }

        try
        {
            using var mail = new MailMessage
            {
                From = new MailAddress(_options.FromAddress, _options.FromName),
                Subject = message.Subject,
                Body = message.HtmlBody,
                IsBodyHtml = true
            };
            mail.To.Add(message.To);
            mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(message.TextBody, null, "text/plain"));

            using var client = new SmtpClient(_options.SmtpHost, _options.SmtpPort)
            {
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout = 10000
            };
            if (!string.IsNullOrEmpty(_options.SmtpUser))
            {
                client.Credentials = new NetworkCredential(_options.SmtpUser, _options.SmtpPassword);
            }

            await client.SendMailAsync(mail, cancellationToken);
            return true;
        }
        catch (Exception ex) when (ex is SmtpException or InvalidOperationException or FormatException)
        {
            _logger.LogWarning(ex, "No se pudo enviar el correo \"{Subject}\".", message.Subject);
            return false;
        }
    }
}
