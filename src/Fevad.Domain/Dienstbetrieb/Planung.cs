using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Dienstbetrieb;

public enum Wiederholung
{
    [Display(Name = "wöchentlich")] Woechentlich,
    [Display(Name = "alle 2 Wochen")] Zweiwoechentlich,
    [Display(Name = "alle 4 Wochen")] Vierwoechentlich,
    [Display(Name = "monatlich, gleicher Wochentag")] MonatlichWochentag,
}

/// <summary>
/// Serie wiederkehrender Dienste. Die einzelnen Termine werden beim Anlegen erzeugt und sind danach
/// eigenständig: ein Termin kann verschoben oder abgesagt werden, ohne die Serie zu berühren.
/// </summary>
public class DienstSerie : Entitaet
{
    public Wiederholung Rhythmus { get; set; }
    public DateOnly ErsterTag { get; set; }
    public DateOnly LetzterTag { get; set; }

    public List<Dienst> Dienste { get; set; } = [];

    /// <summary>Alle Tage der Serie von <paramref name="erster"/> bis einschließlich <paramref name="letzter"/>.</summary>
    public static IEnumerable<DateOnly> Tage(Wiederholung rhythmus, DateOnly erster, DateOnly letzter)
    {
        if (rhythmus == Wiederholung.MonatlichWochentag)
        {
            // Der wievielte Wochentag im Monat (2. Donnerstag); ein 5. gilt als „letzter“.
            var nummer = (erster.Day - 1) / 7 + 1;
            for (var monat = new DateOnly(erster.Year, erster.Month, 1); monat <= letzter; monat = monat.AddMonths(1))
            {
                var tag = NterWochentag(monat, erster.DayOfWeek, nummer);
                if (tag >= erster && tag <= letzter)
                {
                    yield return tag;
                }
            }

            yield break;
        }

        var schritt = rhythmus switch
        {
            Wiederholung.Zweiwoechentlich => 14,
            Wiederholung.Vierwoechentlich => 28,
            _ => 7,
        };
        for (var tag = erster; tag <= letzter; tag = tag.AddDays(schritt))
        {
            yield return tag;
        }
    }

    private static DateOnly NterWochentag(DateOnly monatsanfang, DayOfWeek wochentag, int nummer)
    {
        var erster = monatsanfang.AddDays(((int)wochentag - (int)monatsanfang.DayOfWeek + 7) % 7);
        if (nummer < 5)
        {
            return erster.AddDays(7 * (nummer - 1));
        }

        var letzter = erster.AddDays(28);
        return letzter.Month == monatsanfang.Month ? letzter : erster.AddDays(21);
    }
}

public enum RueckmeldungAntwort
{
    [Display(Name = "zugesagt")] Zusage,
    [Display(Name = "abgesagt")] Absage,
}

/// <summary>Zu- oder Absage eines Mitglieds zu einem geplanten Dienst. Wird bei der Anwesenheit als Vorschlag übernommen.</summary>
public class Rueckmeldung : Entitaet
{
    public Guid DienstId { get; set; }
    public Dienst? Dienst { get; set; }

    public Guid MitgliedId { get; set; }
    public Mitglied? Mitglied { get; set; }

    public RueckmeldungAntwort Antwort { get; set; }

    /// <summary>Freiwillige Begründung einer Absage, nur für Führungskräfte sichtbar. Oft gesundheitlich, deshalb nicht im Protokoll.</summary>
    [NichtProtokollieren]
    public string? Begruendung { get; set; }

    public DateTimeOffset GemeldetAm { get; set; }
}

/// <summary>
/// Was über ein Mitglied bekannt sein muss, um zu entscheiden, ob ein Dienst es betrifft:
/// Abteilung am Tag des Dienstes, Gruppen und Qualifikationen.
/// </summary>
public sealed class Teilnehmerprofil(
    IReadOnlyList<Mitgliedschaft> mitgliedschaften,
    IReadOnlySet<Guid> gruppen,
    IReadOnlyList<(Guid QualifikationId, DateOnly ErworbenAm, DateOnly? GueltigBis)> qualifikationen)
{
    public bool Betrifft(Dienst dienst, DateOnly tag)
    {
        var mitgliedschaft = mitgliedschaften.FirstOrDefault(m => m.IstAktivAm(tag) && m.Status != MitgliedStatus.Ausgeschieden);
        if (mitgliedschaft is null)
        {
            return false;
        }

        if (dienst.Abteilungen.Count > 0 && !mitgliedschaften.Any(m => dienst.Abteilungen.Any(a => a.Id == m.AbteilungId) && m.IstAktivAm(tag)))
        {
            return false;
        }

        if (dienst.GruppeId is { } gruppe && !gruppen.Contains(gruppe))
        {
            return false;
        }

        return dienst.QualifikationId is not { } qualifikation
            || qualifikationen.Any(q => q.QualifikationId == qualifikation && q.ErworbenAm <= tag && (q.GueltigBis is null || q.GueltigBis >= tag));
    }
}
