namespace Fevad.Infrastructure.Mail;

/// <summary>SMTP-Einstellungen für den Mailversand aus dem Konfigurationsabschnitt <c>Mail</c>.</summary>
public sealed class MailOptionen
{
    /// <summary>Name des Konfigurationsabschnitts.</summary>
    public const string Abschnitt = "Mail";

    /// <summary>Leer = kein Mailversand; Einrichtungslinks gibt dann der Administrator von Hand weiter.</summary>
    public string? SmtpServer { get; set; }
    /// <summary>SMTP-Port; Standard 587 (Submission mit STARTTLS).</summary>
    public int Port { get; set; } = 587;

    /// <summary>STARTTLS erzwingen. Nur für einen lokalen Testserver (Mailpit) abschalten.</summary>
    public bool StartTls { get; set; } = true;
    /// <summary>Benutzername für die SMTP-Anmeldung; leer heißt ohne Anmeldung.</summary>
    public string? Benutzername { get; set; }
    /// <summary>Passwort für die SMTP-Anmeldung; gehört in die Geheimnisverwaltung, nicht in appsettings.</summary>
    public string? Passwort { get; set; }
    /// <summary>Absenderadresse der Mails.</summary>
    public string Absender { get; set; } = "";
    /// <summary>Anzeigename des Absenders.</summary>
    public string AbsenderName { get; set; } = "FEVAD · FF Adelberg";

    /// <summary>Mailversand ist eingerichtet: Server und Absender sind gesetzt.</summary>
    public bool Aktiv => !string.IsNullOrWhiteSpace(SmtpServer) && !string.IsNullOrWhiteSpace(Absender);
}
