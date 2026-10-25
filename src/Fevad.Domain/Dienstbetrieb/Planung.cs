using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Dienstbetrieb;

/// <summary>Rhythmus einer Dienstserie.</summary>
public enum Wiederholung
{
    /// <summary>Jede Woche am selben Wochentag.</summary>
    [Display(Name = "wöchentlich")] Woechentlich,
    /// <summary>Alle zwei Wochen.</summary>
    [Display(Name = "alle 2 Wochen")] Zweiwoechentlich,
    /// <summary>Alle vier Wochen.</summary>
    [Display(Name = "alle 4 Wochen")] Vierwoechentlich,
    /// <summary>Jeden Monat am gleichen Wochentag, z. B. am 2. Donnerstag; ein 5. Wochentag gilt als letzter.</summary>
    [Display(Name = "monatlich, gleicher Wochentag")] MonatlichWochentag,
}

/// <summary>
/// Serie wiederkehrender Dienste. Die einzelnen Termine werden beim Anlegen erzeugt und sind danach
/// eigenständig: ein Termin kann verschoben oder abgesagt werden, ohne die Serie zu berühren.
/// </summary>
public class DienstSerie : Entitaet
{
    /// <summary>Rhythmus der Serie.</summary>
    public Wiederholung Rhythmus { get; set; }
    /// <summary>Tag des ersten Termins.</summary>
    public DateOnly ErsterTag { get; set; }
    /// <summary>Letzter möglicher Tag eines Termins.</summary>
    public DateOnly LetzterTag { get; set; }

    /// <summary>Die erzeugten Termine.</summary>
    public List<Dienst> Dienste { get; set; } = [];

    /// <summary>Alle Tage der Serie von <paramref name="erster"/> bis einschließlich <paramref name="letzter"/>.</summary>
    /// <param name="rhythmus">Rhythmus der Serie.</param>
    /// <param name="erster">Erster Tag; bestimmt Wochentag und bei monatlich die Nummer im Monat.</param>
    /// <param name="letzter">Letzter möglicher Tag.</param>
    /// <returns>Die Tage in aufsteigender Folge.</returns>
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

/// <summary>Antwort eines Mitglieds auf einen geplanten Dienst.</summary>
public enum RueckmeldungAntwort
{
    /// <summary>Zugesagt.</summary>
    [Display(Name = "zugesagt")] Zusage,
    /// <summary>Abgesagt.</summary>
    [Display(Name = "abgesagt")] Absage,
}

/// <summary>Zu- oder Absage eines Mitglieds zu einem geplanten Dienst. Wird bei der Anwesenheit als Vorschlag übernommen.</summary>
public class Rueckmeldung : Entitaet
{
    /// <summary>Schlüssel des Dienstes.</summary>
    public Guid DienstId { get; set; }
    /// <summary>Der Dienst.</summary>
    public Dienst? Dienst { get; set; }

    /// <summary>Schlüssel des Mitglieds.</summary>
    public Guid MitgliedId { get; set; }
    /// <summary>Das Mitglied.</summary>
    public Mitglied? Mitglied { get; set; }

    /// <summary>Zusage oder Absage.</summary>
    public RueckmeldungAntwort Antwort { get; set; }

    /// <summary>Freiwillige Begründung einer Absage, nur für Führungskräfte sichtbar. Oft gesundheitlich, deshalb nicht im Protokoll.</summary>
    [NichtProtokollieren]
    public string? Begruendung { get; set; }

    /// <summary>Zeitpunkt der letzten Rückmeldung in UTC.</summary>
    public DateTimeOffset GemeldetAm { get; set; }

    /// <summary>Die Absage stammt aus dieser gemeldeten Abwesenheit und fällt weg, wenn die Abwesenheit zurückgenommen wird.</summary>
    public Guid? AbwesenheitId { get; set; }
    /// <summary>Die Abwesenheit, aus der die Absage stammt.</summary>
    public Abwesenheit? Abwesenheit { get; set; }
}

/// <summary>
/// Was über ein Mitglied bekannt sein muss, um zu entscheiden, ob ein Dienst es betrifft:
/// Abteilung am Tag des Dienstes, Gruppen und Qualifikationen.
/// </summary>
/// <param name="mitgliedschaften">Mitgliedschaften des Mitglieds.</param>
/// <param name="gruppen">Schlüssel der Gruppen, in denen das Mitglied ist.</param>
/// <param name="qualifikationen">Qualifikationen mit Erwerb und Ablauf.</param>
public sealed class Teilnehmerprofil(
    IReadOnlyList<Mitgliedschaft> mitgliedschaften,
    IReadOnlySet<Guid> gruppen,
    IReadOnlyList<(Guid QualifikationId, DateOnly ErworbenAm, DateOnly? GueltigBis)> qualifikationen)
{
    /// <summary>Prüft, ob der Dienst das Mitglied an einem Tag betrifft: Abteilung, Gruppe und Qualifikation müssen passen.</summary>
    /// <param name="dienst">Der Dienst mit geladenen Abteilungen.</param>
    /// <param name="tag">Tag des Dienstes.</param>
    /// <returns>true, wenn das Mitglied zur Zielgruppe gehört.</returns>
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
