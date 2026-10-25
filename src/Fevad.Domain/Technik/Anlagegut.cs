using System.ComponentModel.DataAnnotations;

namespace Fevad.Domain.Technik;

/// <summary>Wem ein Gerät oder Fahrzeug gehört (V60, Anlagennachweis nach GemHVO §§ 37, 38).</summary>
public enum Eigentuemer
{
    /// <summary>Gemeindeeigentum; erscheint im Anlagennachweis der Kämmerei.</summary>
    [Display(Name = "Gemeinde")] Gemeinde,

    /// <summary>Aus der Kameradschaftskasse bezahlt; nicht im Anlagennachweis der Gemeinde.</summary>
    [Display(Name = "Kameradschaftskasse")] Kameradschaftskasse,

    /// <summary>Leihgabe von Land oder Landkreis, z. B. Katastrophenschutz; bleibt fremdes Eigentum.</summary>
    [Display(Name = "Leihgabe Land/Kreis")] Leihgabe,
}

/// <summary>Was mit einem ausgesonderten Gerät oder Fahrzeug geschehen ist (V60).</summary>
public enum Verwertung
{
    /// <summary>Entsorgt bzw. verschrottet.</summary>
    [Display(Name = "verschrottet/entsorgt")] Verschrottung,

    /// <summary>Verkauft; der Erlös steht in <see cref="IAnlagegut.Erloes"/>.</summary>
    [Display(Name = "verkauft")] Verkauf,

    /// <summary>Unentgeltlich abgegeben, z. B. an eine andere Feuerwehr oder für Hilfsprojekte.</summary>
    [Display(Name = "unentgeltlich abgegeben")] Abgabe,

    /// <summary>An den Eigentümer zurückgegeben, z. B. eine Leihgabe des Landes.</summary>
    [Display(Name = "an Eigentümer zurückgegeben")] Rueckgabe,

    /// <summary>Verloren oder gestohlen.</summary>
    [Display(Name = "verloren/gestohlen")] Verlust,
}

/// <summary>
/// Angaben für die Anlagenbuchhaltung der Gemeinde und für die Aussonderung, gemeinsam für Geräte und Fahrzeuge (V60).
/// </summary>
public interface IAnlagegut
{
    /// <summary>Nummer in der Anlagenbuchhaltung der Gemeinde (SAP); wird aus der Kämmerei übernommen.</summary>
    string? Anlagennummer { get; set; }

    /// <summary>Anschaffungswert brutto in Euro.</summary>
    decimal? Anschaffungswert { get; }

    /// <summary>Nutzungsdauer in Jahren laut Anlagenbuchhaltung (Abschreibung), nicht die technische Lebensdauer.</summary>
    int? NutzungsdauerJahre { get; set; }

    /// <summary>Eigentümer des Anlageguts.</summary>
    Eigentuemer Eigentuemer { get; set; }

    /// <summary>Erhaltene Fördermittel in Euro, z. B. Z-Feu-Festbetrag.</summary>
    decimal? Foerdermittel { get; set; }

    /// <summary>Fördergeber, z. B. „Land BW (Z-Feu)“.</summary>
    string? Foerdergeber { get; set; }

    /// <summary>Ende der Zweckbindung der Förderung; vorher braucht eine Aussonderung eine Mitteilung an den Fördergeber.</summary>
    DateOnly? ZweckbindungBis { get; set; }

    /// <summary>Art der Verwertung nach der Aussonderung.</summary>
    Verwertung? Verwertung { get; set; }

    /// <summary>Verkaufserlös in Euro.</summary>
    decimal? Erloes { get; set; }

    /// <summary>Verweis auf den Beschluss zur Aussonderung, z. B. „Gemeinderat 12.03.2027, TOP 4“.</summary>
    string? Aussonderungsbeschluss { get; set; }

    /// <summary>Tag, an dem der Fördergeber über die Aussonderung informiert wurde (VwV Z-Feu Nr. 6.5).</summary>
    DateOnly? FoerdergeberMitgeteiltAm { get; set; }
}

/// <summary>Regeln zur Aussonderung und Veräußerung von Anlagegütern (V60).</summary>
public static class Aussonderungsregeln
{
    /// <summary>Ab der Wertgrenze braucht die Aussonderung einen Beschluss; ohne Anschaffungswert gilt sie als nicht erreicht.</summary>
    public static bool BeschlussNoetig(decimal? anschaffungswert, decimal grenze) =>
        anschaffungswert is { } wert && wert >= grenze;

    /// <summary>Fördermittel mit noch laufender Zweckbindung müssen dem Fördergeber gemeldet werden.</summary>
    public static bool FoerdergeberMitteilen(IAnlagegut gut, DateOnly ausgesondertAm) =>
        gut.Foerdermittel is > 0 && gut.ZweckbindungBis is { } bis && bis >= ausgesondertAm;

    /// <summary>Prüft die Angaben zur Aussonderung; liefert Fehlermeldungen je Feld (leer = in Ordnung).</summary>
    public static IReadOnlyList<(string Feld, string Meldung)> Pruefen(Verwertung? verwertung, decimal? erloes, string? grund)
    {
        var fehler = new List<(string, string)>();
        if (verwertung is null)
        {
            fehler.Add((nameof(IAnlagegut.Verwertung), "Bitte angeben, was mit dem Gegenstand geschieht."));
        }

        if (string.IsNullOrWhiteSpace(grund))
        {
            fehler.Add(("Grund", "Bitte die Begründung angeben."));
        }

        if (erloes is < 0)
        {
            fehler.Add((nameof(IAnlagegut.Erloes), "Der Erlös kann nicht negativ sein."));
        }
        else if (erloes is > 0 && verwertung != Verwertung.Verkauf)
        {
            fehler.Add((nameof(IAnlagegut.Erloes), "Einen Erlös gibt es nur bei einem Verkauf."));
        }

        return fehler;
    }
}

/// <summary>Fortlaufende Inventarnummern beim Eingang einer Lieferung (V60).</summary>
public static class Inventarnummern
{
    /// <summary>
    /// Zählt die Ziffern am Ende der Startnummer hoch und behält führende Nullen bei, z. B. „SL-0098“ → „SL-0098“, „SL-0099“, „SL-0100“.
    /// Ohne Ziffern am Ende wird „-1“, „-2“ … angehängt.
    /// </summary>
    public static IReadOnlyList<string> Folge(string start, int anzahl)
    {
        start = start.Trim();
        var ende = start.Length;
        while (ende > 0 && char.IsAsciiDigit(start[ende - 1]))
        {
            ende--;
        }

        if (ende == start.Length)
        {
            return Enumerable.Range(1, anzahl).Select(i => $"{start}-{i}").ToList();
        }

        var praefix = start[..ende];
        var ziffern = start[ende..];
        var zahl = long.Parse(ziffern, System.Globalization.CultureInfo.InvariantCulture);
        return Enumerable.Range(0, anzahl)
            .Select(i => praefix + (zahl + i).ToString(System.Globalization.CultureInfo.InvariantCulture).PadLeft(ziffern.Length, '0'))
            .ToList();
    }
}
