using Fevad.Domain.Dienstbetrieb;

namespace Fevad.Web.Infrastruktur;

/// <summary>Texte rund um Termine des Dienstplans.</summary>
public static class Terminanzeige
{
    /// <summary>„Einsatzabteilung + Jugendfeuerwehr · Gruppe 1 · AGT“ oder „alle“. Erwartet geladene Abteilungen, Gruppe und Qualifikation.</summary>
    public static string Zielgruppe(Dienst dienst)
    {
        var teile = new[]
        {
            dienst.Abteilungen.Count == 0 ? null : string.Join(" + ", dienst.Abteilungen.OrderBy(a => a.Sortierung).Select(a => a.Bezeichnung)),
            dienst.Gruppe?.Bezeichnung,
            dienst.Qualifikation is { } q ? q.Kurzbezeichnung ?? q.Bezeichnung : null,
        }.Where(t => t is not null);
        var text = string.Join(" · ", teile);
        return text.Length == 0 ? "alle" : text;
    }

    /// <summary>„Do, 05.11.2026 · 19:30–21:30 Uhr“, über Mitternacht mit Datum am Ende.</summary>
    public static string Zeitraum(Dienst dienst)
    {
        var beginn = Anzeige.Lokal(dienst.Beginn);
        var ende = Anzeige.Lokal(dienst.Ende);
        var bis = beginn.Date == ende.Date ? Anzeige.Zeit(dienst.Ende) : Anzeige.DatumZeit(dienst.Ende);
        return $"{beginn.ToString("ddd, dd.MM.yyyy", Kultur)} · {Anzeige.Zeit(dienst.Beginn)}–{bis} Uhr";
    }

    private static readonly System.Globalization.CultureInfo Kultur = new("de-DE");
}
