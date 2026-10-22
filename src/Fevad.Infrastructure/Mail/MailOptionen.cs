namespace Fevad.Infrastructure.Mail;

public sealed class MailOptionen
{
    public const string Abschnitt = "Mail";

    /// <summary>Leer = kein Mailversand; Einrichtungslinks gibt dann der Administrator von Hand weiter.</summary>
    public string? SmtpServer { get; set; }
    public int Port { get; set; } = 587;

    /// <summary>STARTTLS erzwingen. Nur für einen lokalen Testserver (Mailpit) abschalten.</summary>
    public bool StartTls { get; set; } = true;
    public string? Benutzername { get; set; }
    public string? Passwort { get; set; }
    public string Absender { get; set; } = "";
    public string AbsenderName { get; set; } = "FEVAD · FF Adelberg";

    public bool Aktiv => !string.IsNullOrWhiteSpace(SmtpServer) && !string.IsNullOrWhiteSpace(Absender);
}
