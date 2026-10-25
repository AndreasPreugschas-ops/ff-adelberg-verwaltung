using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Dienstbetrieb;

/// <summary>
/// Löschkonzept L5: Nach der Frist werden die einzelnen Anwesenheiten eines Kalenderjahres je Mitglied zu einer
/// Jahressumme verdichtet und gelöscht. Die Summe reicht für Dienstzeitbescheinigungen, Ehrungen und Statistik;
/// welcher Dienst an welchem Tag besucht wurde, ist danach nicht mehr nachvollziehbar.
/// </summary>
public class Anwesenheitsjahressumme : Entitaet
{
    public Guid MitgliedId { get; set; }
    public Mitglied? Mitglied { get; set; }

    public int Jahr { get; set; }

    /// <summary>Besuchte Dienste.</summary>
    public int Anwesend { get; set; }
    public int Entschuldigt { get; set; }
    public int Unentschuldigt { get; set; }

    /// <summary>Anwesende Zeit in Minuten, davon die Dienste, die für die Standortfortbildung zählen.</summary>
    public int Minuten { get; set; }
    public int FortbildungMinuten { get; set; }

    /// <summary>Eine einzelne Anwesenheit, wie sie in die Jahressumme eingeht. <paramref name="Jahr"/> ist das Kalenderjahr des Dienstbeginns in Ortszeit.</summary>
    public sealed record Einzelwert(Guid MitgliedId, int Jahr, AnwesenheitStatus Status, TimeSpan Dauer, bool ZaehltFuerFortbildung);

    /// <summary>Rechnet die Einzelwerte in die Summe ein; die Summe kann schon Werte aus einem früheren Lauf enthalten.</summary>
    public void Einrechnen(IEnumerable<Einzelwert> werte)
    {
        foreach (var w in werte)
        {
            if (w.MitgliedId != MitgliedId || w.Jahr != Jahr)
            {
                throw new InvalidOperationException("Der Einzelwert gehört zu einem anderen Mitglied oder Jahr.");
            }

            switch (w.Status)
            {
                case AnwesenheitStatus.Anwesend:
                    Anwesend++;
                    var minuten = (int)Math.Max(0, Math.Round(w.Dauer.TotalMinutes));
                    Minuten += minuten;
                    if (w.ZaehltFuerFortbildung)
                    {
                        FortbildungMinuten += minuten;
                    }

                    break;
                case AnwesenheitStatus.Entschuldigt:
                    Entschuldigt++;
                    break;
                default:
                    Unentschuldigt++;
                    break;
            }
        }
    }

    /// <summary>Verdichtet Einzelwerte zu Summen je Mitglied und Jahr; vorhandene Summen werden fortgeschrieben.</summary>
    public static IReadOnlyList<Anwesenheitsjahressumme> Verdichten(IEnumerable<Einzelwert> werte, IEnumerable<Anwesenheitsjahressumme> vorhandene)
    {
        var summen = vorhandene.ToDictionary(s => (s.MitgliedId, s.Jahr));
        foreach (var gruppe in werte.GroupBy(w => (w.MitgliedId, w.Jahr)))
        {
            if (!summen.TryGetValue(gruppe.Key, out var summe))
            {
                summe = new Anwesenheitsjahressumme { MitgliedId = gruppe.Key.MitgliedId, Jahr = gruppe.Key.Jahr };
                summen[gruppe.Key] = summe;
            }

            summe.Einrechnen(gruppe);
        }

        return summen.Values.ToList();
    }
}
