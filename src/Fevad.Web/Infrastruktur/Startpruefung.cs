namespace Fevad.Web.Infrastruktur;

/// <summary>
/// Prüft beim Start außerhalb der Entwicklung, ob die Konfiguration für den Echtbetrieb taugt. Fehler verhindern den Start,
/// damit ein Server nicht halb eingerichtet läuft (z. B. ohne Passwortlinks). Warnungen landen nur im Log.
/// </summary>
public static class Startpruefung
{
    public sealed record Ergebnis(IReadOnlyList<string> Fehler, IReadOnlyList<string> Warnungen);

    public static Ergebnis Pruefen(IConfiguration konfiguration)
    {
        var fehler = new List<string>();
        var warnungen = new List<string>();

        if (string.IsNullOrWhiteSpace(konfiguration.GetConnectionString("Datenbank")))
        {
            fehler.Add("ConnectionStrings:Datenbank ist nicht gesetzt.");
        }

        var adresse = konfiguration["Anmeldung:OeffentlicheAdresse"];
        if (string.IsNullOrWhiteSpace(adresse))
        {
            fehler.Add("Anmeldung:OeffentlicheAdresse ist nicht gesetzt; ohne sie gibt es keine Einrichtungs- und Passwortlinks.");
        }
        else if (!Uri.TryCreate(adresse, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            fehler.Add($"Anmeldung:OeffentlicheAdresse muss eine vollständige https-Adresse sein, ist aber „{adresse}“.");
        }

        if (konfiguration.GetValue<bool>("Anmeldung:ZweiFaktorPflichtAus"))
        {
            warnungen.Add("Anmeldung:ZweiFaktorPflichtAus ist gesetzt, wirkt außerhalb von Development aber nicht. Die Zwei-Faktor-Pflicht bleibt an; den Eintrag bitte entfernen.");
        }

        if (string.IsNullOrWhiteSpace(konfiguration["Mail:SmtpServer"]) || string.IsNullOrWhiteSpace(konfiguration["Mail:Absender"]))
        {
            warnungen.Add("Mail:SmtpServer oder Mail:Absender fehlt: Es werden keine E-Mails verschickt, Einrichtungslinks gibt der Administrator von Hand weiter.");
        }

        if (!string.IsNullOrWhiteSpace(konfiguration["Seed:Admin:Passwort"]))
        {
            warnungen.Add("Seed:Admin:Passwort ist gesetzt. Nach der ersten Anmeldung des Administrators bitte aus der Konfiguration entfernen.");
        }

        var token = konfiguration["Fe2:WebhookToken"];
        if (!string.IsNullOrEmpty(token) && token.Length < 32)
        {
            warnungen.Add("Fe2:WebhookToken ist kürzer als 32 Zeichen und damit leicht zu erraten.");
        }

        return new Ergebnis(fehler, warnungen);
    }
}
