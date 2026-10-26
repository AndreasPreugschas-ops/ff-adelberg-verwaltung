using System.Globalization;
using System.Text;
using Fevad.Domain.Ausbildung;
using Fevad.Domain.Dienstbetrieb;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;
using Fevad.Infrastructure.Persistenz;
using Microsoft.EntityFrameworkCore;

namespace Fevad.Infrastructure.Auswertungen;

/// <summary>
/// Jahresstatistik der Jugend- und Kinderfeuerwehr zum 31.12. (V49): Mitglieder nach Jahrgang und Geschlecht, Zugänge und Abgänge
/// nach Grund, Betreuer, Dienste mit Stunden, Veranstaltungen und erworbene Abzeichen. Grundlage für die Meldungen an Kreis, Land
/// und DJF sowie für den Jugendbericht in der Hauptversammlung. Enthält nur Zahlen, keine Namen.
/// </summary>
public sealed class Jugendstatistik(AppDbContext db)
{
    private static readonly CultureInfo Deutsch = new("de-DE");

    /// <summary>Mitglieder eines Jahrgangs am Stichtag.</summary>
    /// <param name="Jahrgang">Geburtsjahr.</param>
    /// <param name="Maennlich">Männlich.</param>
    /// <param name="Weiblich">Weiblich.</param>
    /// <param name="Divers">Divers.</param>
    /// <param name="OhneAngabe">Geschlecht nicht angegeben.</param>
    public sealed record Jahrgangszeile(int Jahrgang, int Maennlich, int Weiblich, int Divers, int OhneAngabe)
    {
        /// <summary>Alle Mitglieder des Jahrgangs.</summary>
        public int Gesamt => Maennlich + Weiblich + Divers + OhneAngabe;
    }

    /// <summary>Stand einer Jugend- oder Kinderabteilung im Jahr.</summary>
    /// <param name="Bezeichnung">Name der Abteilung.</param>
    /// <param name="Art">Jugend- oder Kinderfeuerwehr.</param>
    /// <param name="Jahrgaenge">Mitglieder am Stichtag nach Jahrgang, ältester zuerst.</param>
    /// <param name="Zugaenge">Neu aufgenommene Mitglieder im Jahr (ohne Wechsel aus der Kinderfeuerwehr).</param>
    /// <param name="Abgaenge">Abgänge im Jahr nach Grund.</param>
    /// <param name="Betreuer">Funktionsträger der Abteilung am Stichtag (Jugendwart, Stellvertreter, Betreuer).</param>
    public sealed record Gruppe(string Bezeichnung, AbteilungArt Art, IReadOnlyList<Jahrgangszeile> Jahrgaenge, int Zugaenge, IReadOnlyList<Jahresstatistik.Bewegung> Abgaenge, int Betreuer)
    {
        /// <summary>Mitglieder am Stichtag.</summary>
        public int Gesamt => Jahrgaenge.Sum(j => j.Gesamt);
    }

    /// <summary>Dienste einer Art mit Stunden.</summary>
    /// <param name="Art">Art des Dienstes.</param>
    /// <param name="Anzahl">Durchgeführte Dienste.</param>
    /// <param name="Stunden">Dauer aller Dienste.</param>
    /// <param name="Teilnahmen">Anwesenheiten.</param>
    /// <param name="Personenstunden">Summe der Stunden aller Anwesenden.</param>
    public sealed record Dienstzeile(DienstArt Art, int Anzahl, decimal Stunden, int Teilnahmen, decimal Personenstunden);

    /// <summary>Die Jugendstatistik eines Jahres.</summary>
    /// <param name="Jahr">Kalenderjahr.</param>
    /// <param name="Stichtag">31.12. des Jahres.</param>
    /// <param name="Gruppen">Je Jugend- und Kinderabteilung ein Stand.</param>
    /// <param name="Dienste">Durchgeführte Dienste der Jugend- und Kinderfeuerwehr nach Art.</param>
    /// <param name="Abzeichen">Im Jahr erworbene Jugendabzeichen nach Stufe.</param>
    /// <param name="Hinweise">Hinweise zur Vollständigkeit.</param>
    public sealed record Bericht(int Jahr, DateOnly Stichtag, IReadOnlyList<Gruppe> Gruppen, IReadOnlyList<Dienstzeile> Dienste,
        IReadOnlyList<Jahresstatistik.Bewegung> Abzeichen, IReadOnlyList<string> Hinweise);

    /// <summary>Berechnet die Jugendstatistik für ein Kalenderjahr (Ortszeit).</summary>
    /// <param name="jahr">Kalenderjahr.</param>
    /// <param name="ct">Abbruchsignal.</param>
    /// <returns>Der Bericht.</returns>
    public async Task<Bericht> ErstellenAsync(int jahr, CancellationToken ct = default)
    {
        var erster = new DateOnly(jahr, 1, 1);
        var stichtag = new DateOnly(jahr, 12, 31);
        var hinweise = new List<string>();

        var abteilungen = await db.Abteilungen.AsNoTracking().OrderBy(a => a.Sortierung).ToListAsync(ct);
        var jugend = abteilungen.Where(a => a.Art is AbteilungArt.Jugend or AbteilungArt.Kinder).ToList();
        var jugendIds = jugend.Select(a => a.Id).ToHashSet();
        var artVon = abteilungen.ToDictionary(a => a.Id, a => a.Art);

        var mitgliedschaften = await db.Mitgliedschaften.AsNoTracking()
            .Select(m => new { m.MitgliedId, m.AbteilungId, m.Status, m.GueltigAb, m.GueltigBis, m.Austrittsgrund, m.Mitglied!.Geburtsdatum, m.Mitglied.Geschlecht })
            .ToListAsync(ct);

        var gruppen = new List<Gruppe>();
        foreach (var abteilung in jugend)
        {
            var eigene = mitgliedschaften.Where(m => m.AbteilungId == abteilung.Id).ToList();
            var amStichtag = eigene
                .Where(m => m.GueltigAb <= stichtag && (m.GueltigBis is null || m.GueltigBis >= stichtag) && m.Status != MitgliedStatus.Ausgeschieden)
                .DistinctBy(m => m.MitgliedId)
                .ToList();
            var jahrgaenge = amStichtag
                .GroupBy(m => m.Geburtsdatum.Year)
                .OrderBy(g => g.Key)
                .Select(g => new Jahrgangszeile(g.Key,
                    g.Count(m => m.Geschlecht == Geschlecht.Maennlich),
                    g.Count(m => m.Geschlecht == Geschlecht.Weiblich),
                    g.Count(m => m.Geschlecht == Geschlecht.Divers),
                    g.Count(m => m.Geschlecht is null)))
                .ToList();

            // Zugang ist, wer im Jahr beginnt und nicht am Vortag aus einer anderen Jugend- oder Kinderabteilung kam.
            var zugaenge = eigene.Count(m => m.GueltigAb >= erster && m.GueltigAb <= stichtag
                && !mitgliedschaften.Any(v => v.MitgliedId == m.MitgliedId && jugendIds.Contains(v.AbteilungId) && v.AbteilungId != abteilung.Id
                    && v.GueltigBis is { } b && b >= m.GueltigAb.AddDays(-1) && b < m.GueltigAb));

            var abgaenge = eigene
                .Where(m => m.GueltigBis is { } b && b >= erster && b <= stichtag)
                .Select(m =>
                {
                    var nachfolger = mitgliedschaften.FirstOrDefault(n => n.MitgliedId == m.MitgliedId && n.AbteilungId != m.AbteilungId
                        && n.GueltigAb > m.GueltigBis && n.GueltigAb <= m.GueltigBis!.Value.AddDays(1));
                    return nachfolger is null ? Austritt(m.Austrittsgrund)
                        : artVon.GetValueOrDefault(nachfolger.AbteilungId) switch
                        {
                            AbteilungArt.Einsatz => "Übertritt in die Einsatzabteilung",
                            AbteilungArt.Jugend => "Wechsel in die Jugendfeuerwehr",
                            AbteilungArt.Kinder => "Wechsel in die Kinderfeuerwehr",
                            _ => "Wechsel in eine andere Abteilung",
                        };
                })
                .GroupBy(t => t)
                .Select(g => new Jahresstatistik.Bewegung(g.Key, g.Count()))
                .OrderByDescending(b => b.Anzahl)
                .ToList();

            var betreuer = await db.FunktionZuordnungen.AsNoTracking()
                .Where(f => f.GueltigAb <= stichtag && (f.GueltigBis == null || f.GueltigBis >= stichtag)
                    && (f.AbteilungId == abteilung.Id || (f.AbteilungId == null && f.Funktion!.Abteilungsart == abteilung.Art)))
                .Select(f => f.MitgliedId)
                .Distinct()
                .CountAsync(ct);

            gruppen.Add(new Gruppe(abteilung.Bezeichnung, abteilung.Art, jahrgaenge, zugaenge, abgaenge, betreuer));
        }

        var utcVon = Ortszeit.TagesbeginnUtc(erster);
        var utcBis = Ortszeit.TagesbeginnUtc(stichtag.AddDays(1));
        var dienste = await db.Dienste.AsNoTracking()
            .Where(d => d.Status == DienstStatus.Durchgefuehrt && d.Beginn >= utcVon && d.Beginn < utcBis
                && d.Abteilungen.Any(a => a.Art == AbteilungArt.Jugend || a.Art == AbteilungArt.Kinder))
            .Select(d => new
            {
                d.Art,
                d.Beginn,
                d.Ende,
                Anwesend = d.Anwesenheiten.Where(a => a.Status == AnwesenheitStatus.Anwesend).Select(a => new { a.Von, a.Bis }).ToList(),
            })
            .ToListAsync(ct);
        var offen = await db.Dienste.CountAsync(d => d.Status == DienstStatus.Geplant && d.Beginn >= utcVon && d.Ende < utcBis
            && d.Abteilungen.Any(a => a.Art == AbteilungArt.Jugend || a.Art == AbteilungArt.Kinder), ct);
        if (offen > 0)
        {
            hinweise.Add($"{offen} Dienst(e) der Jugend sind noch als geplant markiert und fehlen in den Zahlen.");
        }

        static decimal Stunden(DateTimeOffset von, DateTimeOffset bis) => bis > von ? (decimal)(bis - von).TotalHours : 0;
        var dienstzeilen = dienste
            .GroupBy(d => d.Art)
            .OrderBy(g => g.Key)
            .Select(g => new Dienstzeile(g.Key, g.Count(),
                Math.Round(g.Sum(d => Stunden(d.Beginn, d.Ende)), 1),
                g.Sum(d => d.Anwesend.Count),
                Math.Round(g.Sum(d => d.Anwesend.Sum(a => Stunden(a.Von ?? d.Beginn, a.Bis ?? d.Ende))), 1)))
            .ToList();

        var abzeichen = (await db.QualifikationNachweise.AsNoTracking()
                .Where(n => n.ErworbenAm >= erster && n.ErworbenAm <= stichtag
                    && n.Qualifikation!.Art == QualifikationArt.Abzeichen && n.Qualifikation.AlterNachJahrgang)
                .GroupBy(n => new { n.Qualifikation!.Bezeichnung, n.Qualifikation.Sortierung })
                .Select(g => new { g.Key.Bezeichnung, g.Key.Sortierung, Anzahl = g.Count() })
                .ToListAsync(ct))
            .OrderBy(a => a.Sortierung)
            .Select(a => new Jahresstatistik.Bewegung(a.Bezeichnung, a.Anzahl))
            .ToList();

        if (gruppen.Count == 0)
        {
            hinweise.Add("Es gibt keine Jugend- oder Kinderabteilung.");
        }

        return new Bericht(jahr, stichtag, gruppen, dienstzeilen, abzeichen, hinweise);
    }

    /// <summary>Die Jugendstatistik als CSV (Bereich;Kennzahl;Wert) zum Übertragen in die Meldungen an Kreis, Land und DJF.</summary>
    /// <param name="b">Der berechnete Bericht.</param>
    /// <returns>Die CSV-Datei als Bytes mit BOM.</returns>
    public static byte[] Csv(Bericht b)
    {
        var csv = new StringBuilder();
        void Zeile(string bereich, string kennzahl, string wert) => csv.Append($"{bereich};{kennzahl};{wert}\r\n");

        Zeile("Bereich", "Kennzahl", "Wert");
        Zeile("Allgemein", "Jahr", b.Jahr.ToString(Deutsch));
        foreach (var g in b.Gruppen)
        {
            foreach (var j in g.Jahrgaenge)
            {
                Zeile(g.Bezeichnung, $"Jahrgang {j.Jahrgang} männlich", j.Maennlich.ToString(Deutsch));
                Zeile(g.Bezeichnung, $"Jahrgang {j.Jahrgang} weiblich", j.Weiblich.ToString(Deutsch));
                Zeile(g.Bezeichnung, $"Jahrgang {j.Jahrgang} divers", j.Divers.ToString(Deutsch));
                Zeile(g.Bezeichnung, $"Jahrgang {j.Jahrgang} ohne Angabe", j.OhneAngabe.ToString(Deutsch));
            }

            Zeile(g.Bezeichnung, "Mitglieder 31.12.", g.Gesamt.ToString(Deutsch));
            Zeile(g.Bezeichnung, "Zugänge", g.Zugaenge.ToString(Deutsch));
            foreach (var a in g.Abgaenge)
            {
                Zeile(g.Bezeichnung, $"Abgang: {a.Text}", a.Anzahl.ToString(Deutsch));
            }

            Zeile(g.Bezeichnung, "Betreuer 31.12.", g.Betreuer.ToString(Deutsch));
        }

        foreach (var d in b.Dienste)
        {
            Zeile("Dienste", $"{JahresberichtPdf.DienstArtText(d.Art)}: Anzahl", d.Anzahl.ToString(Deutsch));
            Zeile("Dienste", $"{JahresberichtPdf.DienstArtText(d.Art)}: Personenstunden", d.Personenstunden.ToString("0.0", Deutsch));
        }

        foreach (var a in b.Abzeichen)
        {
            Zeile("Abzeichen", a.Text, a.Anzahl.ToString(Deutsch));
        }

        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(csv.ToString())];
    }

    private static string Austritt(Austrittsgrund? grund) => grund switch
    {
        null => "Austritt ohne Angabe",
        Austrittsgrund.Tod => "Tod",
        Austrittsgrund.Ausschluss => "Ausschluss",
        Austrittsgrund.Wohnortwechsel => "Wohnortwechsel",
        _ => "Austritt",
    };
}
