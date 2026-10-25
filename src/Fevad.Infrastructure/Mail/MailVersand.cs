using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace Fevad.Infrastructure.Mail;

/// <summary>Versendet Mails aus FEVAD, z. B. Einrichtungslinks und Fristerinnerungen.</summary>
public interface IMailVersand
{
    /// <summary>Der Versand ist eingerichtet.</summary>
    bool Aktiv { get; }

    /// <summary>Sendet eine Textmail.</summary>
    /// <param name="empfaenger">Adresse des Empfängers.</param>
    /// <param name="betreff">Betreff.</param>
    /// <param name="text">Inhalt als reiner Text.</param>
    /// <param name="ct">Abbruchsignal.</param>
    Task SendenAsync(string empfaenger, string betreff, string text, CancellationToken ct = default);
}

/// <summary>
/// Versand über den SMTP-Client der Laufzeit (ohne Zusatzpaket). Unterstützt STARTTLS auf Port 587,
/// aber kein implizites TLS auf Port 465.
/// </summary>
public sealed class SmtpMailVersand(IOptions<MailOptionen> optionen) : IMailVersand
{
    private readonly MailOptionen _optionen = optionen.Value;

    /// <inheritdoc />
    public bool Aktiv => _optionen.Aktiv;

    /// <inheritdoc />
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
