using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace Fevad.Infrastructure.Mail;

public interface IMailVersand
{
    bool Aktiv { get; }

    Task SendenAsync(string empfaenger, string betreff, string text, CancellationToken ct = default);
}

/// <summary>
/// Versand über den SMTP-Client der Laufzeit (ohne Zusatzpaket). Unterstützt STARTTLS auf Port 587,
/// aber kein implizites TLS auf Port 465.
/// </summary>
public sealed class SmtpMailVersand(IOptions<MailOptionen> optionen) : IMailVersand
{
    private readonly MailOptionen _optionen = optionen.Value;

    public bool Aktiv => _optionen.Aktiv;

    public async Task SendenAsync(string empfaenger, string betreff, string text, CancellationToken ct = default)
    {
        if (!Aktiv)
        {
            throw new InvalidOperationException("Mailversand ist nicht eingerichtet (Mail:SmtpServer, Mail:Absender).");
        }

        using var nachricht = new MailMessage(new MailAddress(_optionen.Absender, _optionen.AbsenderName), new MailAddress(empfaenger))
        {
            Subject = betreff,
            Body = text,
        };

        using var smtp = new SmtpClient(_optionen.SmtpServer, _optionen.Port) { EnableSsl = _optionen.StartTls };
        if (!string.IsNullOrEmpty(_optionen.Benutzername))
        {
            smtp.Credentials = new NetworkCredential(_optionen.Benutzername, _optionen.Passwort);
        }

        await smtp.SendMailAsync(nachricht, ct);
    }
}
