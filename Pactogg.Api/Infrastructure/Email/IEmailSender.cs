namespace Pactogg.Api.Infrastructure.Email;

public sealed record EmailMessage(string To, string Subject, string TextBody, string HtmlBody);

public interface IEmailSender
{
    /// <summary>Envía el correo. No lanza: devuelve false si no se pudo (el flujo responde igual para no filtrar cuentas).</summary>
    Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
