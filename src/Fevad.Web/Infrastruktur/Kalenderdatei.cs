using System.Globalization;
using System.Text;
using Fevad.Domain.Dienstbetrieb;

namespace Fevad.Web.Infrastruktur;

/// <summary>Erzeugt einen iCalendar-Feed (RFC 5545) für Kalender-Apps. Zeiten in UTC, die App zeigt sie in Ortszeit.</summary>
public static class Kalenderdatei
{
    /// <summary>Ein Termin im Feed.</summary>
    /// <param name="Dienst">Der Dienst.</param>
    /// <param name="Antwort">Eigene Zu- oder Absage, als Status im Kalender.</param>
    public sealed record Termin(Dienst Dienst, RueckmeldungAntwort? Antwort);

    /// <summary>Baut den Kalender-Feed.</summary>
    /// <param name="name">Name des Kalenders.</param>
    /// <param name="termine">Die Termine.</param>
    /// <param name="jetzt">Erzeugungszeitpunkt (DTSTAMP).</param>
    /// <returns>Der Feed als Text mit CRLF-Zeilenenden.</returns>
    public static string Erzeugen(string name, IEnumerable<Termin> termine, DateTimeOffset jetzt)
    {
        var text = new StringBuilder();
        Zeile(text, "BEGIN:VCALENDAR");
        Zeile(text, "VERSION:2.0");
        Zeile(text, "PRODID:-//FEVAD//Dienstplan//DE");
        Zeile(text, "CALSCALE:GREGORIAN");
        Zeile(text, "METHOD:PUBLISH");
        Zeile(text, "X-WR-CALNAME:" + Escape(name));
        Zeile(text, "X-WR-TIMEZONE:Europe/Berlin");
        Zeile(text, "REFRESH-INTERVAL;VALUE=DURATION:PT4H");
        Zeile(text, "X-PUBLISHED-TTL:PT4H");

        foreach (var (dienst, antwort) in termine)
        {
            var titel = dienst.Status == DienstStatus.Abgesagt ? $"Abgesagt: {dienst.Titel}" : dienst.Titel;
            var beschreibung = new List<string> { Anzeige.Text(dienst.Art) };
            if (!string.IsNullOrWhiteSpace(dienst.Thema))
            {
                beschreibung.Add(dienst.Thema);
            }

            if (antwort is not null)
            {
                beschreibung.Add("Deine Rückmeldung: " + Anzeige.Text(antwort));
            }
            else if (dienst.RueckmeldungErbeten && dienst.Status == DienstStatus.Geplant)
            {
                beschreibung.Add("Bitte in FEVAD zu- oder absagen.");
            }

            Zeile(text, "BEGIN:VEVENT");
            Zeile(text, $"UID:{dienst.Id}@fevad");
            Zeile(text, "DTSTAMP:" + Utc(jetzt));
            Zeile(text, "LAST-MODIFIED:" + Utc(dienst.GeaendertAm ?? dienst.ErstelltAm));
            Zeile(text, "DTSTART:" + Utc(dienst.Beginn));
            Zeile(text, "DTEND:" + Utc(dienst.Ende));
            Zeile(text, "SUMMARY:" + Escape(titel));
            Zeile(text, "DESCRIPTION:" + Escape(string.Join("\n", beschreibung)));
            if (!string.IsNullOrWhiteSpace(dienst.Ort))
            {
                Zeile(text, "LOCATION:" + Escape(dienst.Ort));
            }

            Zeile(text, "STATUS:" + (dienst.Status == DienstStatus.Abgesagt ? "CANCELLED" : antwort == RueckmeldungAntwort.Zusage ? "CONFIRMED" : "TENTATIVE"));
            if (antwort == RueckmeldungAntwort.Absage)
            {
                Zeile(text, "TRANSP:TRANSPARENT");
            }

            Zeile(text, "END:VEVENT");
        }

        Zeile(text, "END:VCALENDAR");
        return text.ToString();
    }

    private static string Utc(DateTimeOffset zeit) => zeit.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

    private static string Escape(string wert) =>
        wert.Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace("\r\n", "\\n").Replace("\n", "\\n");

    /// <summary>Zeilen über 75 Byte werden gefaltet (Fortsetzung beginnt mit Leerzeichen), Zeilenende CRLF.</summary>
    private static void Zeile(StringBuilder text, string zeile)
    {
        var bytes = 0;
        foreach (var zeichen in zeile.EnumerateRunes())
        {
            var laenge = zeichen.Utf8SequenceLength;
            if (bytes + laenge > 75)
            {
                text.Append("\r\n ");
                bytes = 1;
            }

            text.Append(zeichen.ToString());
            bytes += laenge;
        }

        text.Append("\r\n");
    }
}
