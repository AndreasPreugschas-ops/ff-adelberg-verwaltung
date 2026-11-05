using System.Text;
using Fevad.Domain.Mitglieder;

namespace Fevad.Infrastructure.Auswertungen;

/// <summary>
/// Schnittstelle zu ATLAS (LAZ-Übungsprogramm): Personen als CSV im Format, das der ATLAS-Import ohne Nacharbeit zuordnet.
/// Nur Anrede, Name, Geburtsdatum, Personalnummer und, wenn erlaubt, E-Mail und Mobilnummer; keine Anschrift, keine Gesundheitsdaten.
/// </summary>
public static class AtlasExport
{
    /// <summary>Kennung im Änderungsprotokoll (wer, wann, wie viele Personen; die Datei selbst wird nicht gespeichert).</summary>
    public const string Protokolltabelle = "export.atlas";

    /// <summary>Kopfzeile; die Spaltennamen erkennt der ATLAS-Import automatisch.</summary>
    public const string Kopf = "Personalnummer;Anrede;Vorname;Nachname;Geburtsdatum;E-Mail;Mobil";

    /// <summary>Baut die CSV-Datei (UTF-8 mit BOM, Semikolon).</summary>
    /// <param name="mitglieder">Die zu übergebenden Mitglieder.</param>
    /// <param name="mitKontakt">E-Mail und Telefon mitgeben (nur mit dem Recht, Mitglieder zu lesen).</param>
    /// <returns>Dateiinhalt.</returns>
    public static byte[] Csv(IEnumerable<Mitglied> mitglieder, bool mitKontakt)
    {
        var csv = new StringBuilder(Kopf).Append("\r\n");
        foreach (var m in mitglieder)
        {
            var mail = mitKontakt ? m.Email ?? "" : "";
            var telefon = mitKontakt ? m.TelefonMobil ?? m.TelefonFestnetz ?? "" : "";
            csv.Append($"{Feld(m.Personalnummer)};{Feld(m.Anrede ?? "")};{Feld(m.Vorname)};{Feld(m.Nachname)};{m.Geburtsdatum:dd.MM.yyyy};{Feld(mail)};{Feld(telefon)}\r\n");
        }

        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(csv.ToString())];
    }

    /// <summary>Dateiname mit Datum, z. B. <c>ATLAS-Personen-2026-11-01.csv</c>.</summary>
    /// <param name="heute">Datum der Ausgabe.</param>
    /// <returns>Dateiname.</returns>
    public static string Dateiname(DateOnly heute) => $"ATLAS-Personen-{heute:yyyy-MM-dd}.csv";

    // Schutz vor Formeln beim Öffnen in Excel; ATLAS entfernt das führende Hochkomma beim Import wieder.
    /// <summary>Schützt einen CSV-Wert gegen Formeln (Hochkomma vor =, +, -, @) und schließt Trenner und Anführungszeichen ein.</summary>
    private static string Feld(string wert)
    {
        if (wert.Length > 0 && wert[0] is '=' or '+' or '-' or '@')
        {
            wert = "'" + wert;
        }

        return wert.IndexOfAny([';', '"', '\r', '\n']) >= 0 ? $"\"{wert.Replace("\"", "\"\"")}\"" : wert;
    }
}
