using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Gremien;

/// <summary>Turnus einer wiederkehrenden Führungspflicht (V51).</summary>
public enum PflichtTurnus
{
    /// <summary>Einmal im Jahr am Stichtag.</summary>
    [Display(Name = "jährlich")] Jaehrlich,
    /// <summary>Zweimal im Jahr: am Stichtag und sechs Monate später.</summary>
    [Display(Name = "halbjährlich")] Halbjaehrlich,
    /// <summary>Alle paar Jahre am Stichtag, ausgehend von einem Bezugsjahr.</summary>
    [Display(Name = "mehrjährlich")] Mehrjaehrlich,
}

/// <summary>
/// Wiederkehrende Pflicht aus dem Jahreskreis der Führung (V51), z. B. Jahresstatistik im Januar oder Z-Feu-Antrag zum 15.02.
/// Verantwortlich ist eine Funktion, nicht eine Person: wer die Funktion übernimmt, bekommt die Pflicht in „Was liegt an“.
/// </summary>
public class Fuehrungspflicht : Katalogeintrag
{
    /// <summary>Turnus.</summary>
    public PflichtTurnus Turnus { get; set; }

    /// <summary>Monat des Stichtags (1 bis 12).</summary>
    public int Monat { get; set; } = 1;

    /// <summary>Tag des Stichtags; in kürzeren Monaten gilt der Monatsletzte.</summary>
    public int Tag { get; set; } = 1;

    /// <summary>Abstand in Jahren bei <see cref="PflichtTurnus.Mehrjaehrlich"/>, z. B. 5 für den Bedarfsplan.</summary>
    public int IntervallJahre { get; set; } = 1;

    /// <summary>Ein Jahr, in dem die mehrjährliche Pflicht fällig ist, z. B. 2031; leer bei jährlichem Turnus.</summary>
    public int? Bezugsjahr { get; set; }

    /// <summary>Wie viele Tage vor dem Stichtag die Pflicht in „Was liegt an“ erscheint.</summary>
    public int VorlaufTage { get; set; } = 30;

    /// <summary>Wie viele Tage nach dem Stichtag ein versäumter Termin noch rot angezeigt wird; danach verfällt er.</summary>
    public int NachlaufTage { get; set; } = 60;

    /// <summary>Verantwortliche Funktionen als Codes, Alternativen mit „|“, z. B. „KDT|SF“.</summary>
    public string? Verantwortlich { get; set; }

    /// <summary>Seite in FEVAD, auf der die Pflicht erledigt wird, z. B. „/Auswertungen/Jahresstatistik“.</summary>
    public string? Vorlage { get; set; }

    /// <summary>Rechtsgrundlage, z. B. „VwV Z-Feu Nr. 6“.</summary>
    public string? Rechtsgrundlage { get; set; }

    /// <summary>Hinweis für den Nachfolger, was zu tun ist.</summary>
    public string? Hinweis { get; set; }

    /// <summary>Vermerke, wann die Pflicht erledigt wurde.</summary>
    public List<PflichtErledigung> Erledigungen { get; set; } = [];

    /// <summary>Codes der verantwortlichen Funktionen.</summary>
    public IReadOnlyList<string> Funktionscodes =>
        (Verantwortlich ?? "").Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>Stichtage in einem Kalenderjahr.</summary>
    /// <param name="jahr">Das Jahr.</param>
    /// <returns>Null, ein oder zwei Stichtage, aufsteigend.</returns>
    public IEnumerable<DateOnly> StichtageIm(int jahr)
    {
        switch (Turnus)
        {
            case PflichtTurnus.Jaehrlich:
                yield return Datum(jahr, Monat);
                break;
            case PflichtTurnus.Halbjaehrlich:
                var zweiter = (Monat + 5) % 12 + 1;
                var termine = new[] { Datum(jahr, Monat), Datum(jahr, zweiter) }.OrderBy(d => d);
                foreach (var t in termine)
                {
                    yield return t;
                }

                break;
            case PflichtTurnus.Mehrjaehrlich:
                var intervall = Math.Max(1, IntervallJahre);
                if (((jahr - (Bezugsjahr ?? jahr)) % intervall + intervall) % intervall == 0)
                {
                    yield return Datum(jahr, Monat);
                }

                break;
        }
    }

    /// <summary>
    /// Der aktuelle Termin: der späteste Stichtag, dessen Vorlauf begonnen hat und dessen Nachlauf noch nicht vorbei ist. Ältere Termine
    /// verfallen, damit sich nach einer Neuinstallation oder einem versäumten Jahr nichts aufstaut.
    /// </summary>
    /// <param name="heute">Heutiges Datum.</param>
    /// <returns>Der Stichtag oder <c>null</c>, wenn gerade nichts ansteht.</returns>
    public DateOnly? AktuellerTermin(DateOnly heute)
    {
        var grenze = heute.AddDays(Math.Max(0, VorlaufTage));
        var jahre = Turnus == PflichtTurnus.Mehrjaehrlich ? Math.Max(1, IntervallJahre) : 1;
        return Enumerable.Range(grenze.Year - jahre, jahre + 1)
            .SelectMany(StichtageIm)
            .Where(d => d <= grenze)
            .Select(d => (DateOnly?)d)
            .LastOrDefault() is { } termin && heute <= termin.AddDays(Math.Max(0, NachlaufTage)) ? termin : null;
    }

    /// <summary>Nächster Stichtag ab einem Tag (für die Jahresübersicht).</summary>
    /// <param name="ab">Erster möglicher Tag.</param>
    /// <returns>Der Stichtag oder <c>null</c>.</returns>
    public DateOnly? NaechsterTermin(DateOnly ab)
    {
        var jahre = Turnus == PflichtTurnus.Mehrjaehrlich ? Math.Max(1, IntervallJahre) : 1;
        return Enumerable.Range(ab.Year, jahre + 1).SelectMany(StichtageIm).Where(d => d >= ab).Select(d => (DateOnly?)d).FirstOrDefault();
    }

    /// <summary>Ampel des aktuellen Termins: rot nach dem Stichtag, gelb im Vorlauf, <c>null</c>, wenn erledigt oder nichts ansteht.</summary>
    /// <param name="heute">Heutiges Datum.</param>
    /// <returns>Die Ampel oder <c>null</c>.</returns>
    public Fristampel? Ampel(DateOnly heute) =>
        !Aktiv || AktuellerTermin(heute) is not { } termin || Erledigungen.Any(e => e.Stichtag == termin) ? null
        : termin < heute ? Fristampel.Rot
        : Fristampel.Gelb;

    private DateOnly Datum(int jahr, int monat)
    {
        var m = Math.Clamp(monat, 1, 12);
        return new DateOnly(jahr, m, Math.Clamp(Tag, 1, DateTime.DaysInMonth(jahr, m)));
    }
}

/// <summary>Vermerk, dass eine Führungspflicht zu einem Stichtag erledigt ist; bleibt als Gedächtnis für den Nachfolger stehen.</summary>
public class PflichtErledigung : Entitaet
{
    /// <summary>Kennung der Pflicht.</summary>
    public Guid PflichtId { get; set; }
    /// <summary>Navigationsverweis auf die Pflicht.</summary>
    public Fuehrungspflicht? Pflicht { get; set; }

    /// <summary>Stichtag, für den erledigt wurde.</summary>
    public DateOnly Stichtag { get; set; }

    /// <summary>Tag der Erledigung.</summary>
    public DateOnly ErledigtAm { get; set; }

    /// <summary>Name dessen, der erledigt hat, zum Zeitpunkt der Erledigung.</summary>
    public string? ErledigtVon { get; set; }

    /// <summary>Bemerkung, z. B. „an KBM per Mail, Az. 123“.</summary>
    public string? Bemerkung { get; set; }
}
