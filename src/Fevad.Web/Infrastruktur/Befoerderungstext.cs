using Fevad.Domain.Laufbahn;

namespace Fevad.Web.Infrastruktur;

/// <summary>Lesbare Beschreibung eines Beförderungswegs, z. B. „3 Jahre im Dienstgrad, TM2, eine von MA/AGT/SPRF“.</summary>
public static class Befoerderungstext
{
    /// <summary>Beschreibt die Voraussetzungen eines Beförderungswegs in einer Zeile.</summary>
    /// <param name="weg">Der Beförderungsweg.</param>
    /// <returns>Der Text.</returns>
    public static string Weg(Befoerderungsweg weg)
    {
        var teile = new List<string>();
        if (weg.ProbezeitBeendet)
        {
            teile.Add("Probezeit beendet");
        }

        if (weg.MindestjahreImDienstgrad > 0)
        {
            teile.Add($"{weg.MindestjahreImDienstgrad} Jahre im Dienstgrad");
        }

        if (weg.AlleQualifikationen.Count > 0)
        {
            teile.Add(string.Join(", ", weg.AlleQualifikationen));
        }

        if (weg.EineDerQualifikationen.Count > 0)
        {
            teile.Add("eine von " + string.Join("/", weg.EineDerQualifikationen));
        }

        if (weg.UeberspringenErlaubt)
        {
            teile.Add("aus jedem niedrigeren Dienstgrad");
        }

        if (weg.NurVonHand)
        {
            teile.Add("nur von Hand");
        }

        var text = teile.Count == 0 ? "Mindestwartezeit" : string.Join(" · ", teile);
        return weg.Bezeichnung is null ? text : $"{weg.Bezeichnung}: {text}";
    }
}
