using System.Net;
using System.Net.Mail;
using DotNetAuth.Application.IdentityAccess;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNetAuth.Infrastructure.IdentityAccess.Email;

public sealed class SmtpEmailSender(
    IOptions<SmtpOptions> options,
    ILogger<SmtpEmailSender> logger) : IConfirmationEmailSender
{
    private readonly SmtpOptions _options = options.Value;

    public async Task SendAsync(
        ConfirmationEmail email,
        CancellationToken cancellationToken)
    {
        using var message = new MailMessage(_options.FromAddress, email.Recipient)
        {
            Subject = "Confirme seu e-mail",
            IsBodyHtml = true,
            Body = $"<p>Olá, {WebUtility.HtmlEncode(email.DisplayName)}.</p>" +
                $"<p>Confirme seu cadastro acessando <a href=\"{WebUtility.HtmlEncode(email.ConfirmationLink)}\">este link</a>.</p>"
        };

        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.EnableSsl
        };

        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            client.Credentials = new NetworkCredential(_options.Username, _options.Password);
        }

        logger.LogInformation("Sending confirmation email to {Recipient}", email.Recipient);
        await client.SendMailAsync(message, cancellationToken);
    }
}
