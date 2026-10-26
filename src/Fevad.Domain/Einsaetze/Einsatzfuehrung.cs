using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Einsaetze;

/// <summary>Führungsstufe nach FwDV 100 Nr. 3.3.1.3 (V14).</summary>
public enum Fuehrungsstufe
{
    /// <summary>Führen ohne Führungseinheit (Einheitsführer).</summary>
    [Display(Name = "A: ohne Führungseinheit")] A,
    /// <summary>Führen mit örtlichen Führungseinheiten (Führungstrupp, Zugtrupp).</summary>
    [Display(Name = "B: mit örtlicher Führungseinheit")] B,
    /// <summary>Führen mit einer Führungsgruppe.</summary>
    [Display(Name = "C: mit Führungsgruppe")] C,
    /// <summary>Führen mit einer Führungsstaffel oder einem Stab.</summary>
    [Display(Name = "D: mit Führungsstab")] D,
}

/// <summary>Übergabe der Einsatzleitung (V14): wer ab wann geleitet hat, auch an den Kreisbrandmeister oder eine andere Feuerwehr.</summary>
public class Einsatzleitung : Entitaet
{
    /// <summary>Kennung des Einsatzes.</summary>
    public Guid EinsatzId { get; set; }
    /// <summary>Navigationsverweis auf den Einsatz.</summary>
    public Einsatz? Einsatz { get; set; }

    /// <summary>Übernahme der Einsatzleitung.</summary>
    public DateTimeOffset Ab { get; set; }

    /// <summary>Einsatzleiter aus der eigenen Feuerwehr.</summary>
    public Guid? MitgliedId { get; set; }
    /// <summary>Navigationsverweis auf das Mitglied.</summary>
    public Mitglied? Mitglied { get; set; }

    /// <summary>Externer Einsatzleiter, z. B. „KBM Müller“ oder „Kommandant FF Börtlingen“; leer bei eigenem Mitglied.</summary>
    public string? Extern { get; set; }

    /// <summary>Bemerkung zur Übergabe.</summary>
    public string? Bemerkung { get; set; }
}

/// <summary>Einsatzabschnitt mit Abschnittsleiter (FwDV 100, V14).</summary>
public class Einsatzabschnitt : Entitaet
{
    /// <summary>Kennung des Einsatzes.</summary>
    public Guid EinsatzId { get; set; }
    /// <summary>Navigationsverweis auf den Einsatz.</summary>
    public Einsatz? Einsatz { get; set; }

    /// <summary>Bezeichnung, z. B. „EA 1 Brandbekämpfung Nord“.</summary>
    public required string Bezeichnung { get; set; }

    /// <summary>Abschnittsleiter aus der eigenen Feuerwehr.</summary>
    public Guid? LeiterId { get; set; }
    /// <summary>Navigationsverweis auf den Abschnittsleiter.</summary>
    public Mitglied? Leiter { get; set; }

    /// <summary>Externer Abschnittsleiter; leer bei eigenem Mitglied.</summary>
    public string? LeiterExtern { get; set; }

    /// <summary>Auftrag des Abschnitts.</summary>
    public string? Auftrag { get; set; }
}

/// <summary>Art eines Eintrags im Einsatztagebuch (FwDV 100 Anlage 5).</summary>
public enum TagebuchArt
{
    /// <summary>Eintreffen einer Einheit an der Einsatzstelle.</summary>
    [Display(Name = "Eintreffmeldung")] Eintreffmeldung,
    /// <summary>Lagemeldung an die Leitstelle oder Einsatzleitung.</summary>
    [Display(Name = "Lagemeldung")] Lagemeldung,
    /// <summary>Nachforderung von Kräften oder Mitteln.</summary>
    [Display(Name = "Nachforderung")] Nachforderung,
    /// <summary>Befehl oder Maßnahme.</summary>
    [Display(Name = "Maßnahme")] Massnahme,
    /// <summary>Übergabe der Einsatzleitung oder Abschnittsbildung.</summary>
    [Display(Name = "Führung")] Fuehrung,
    /// <summary>Einsatzende, Rückfahrt.</summary>
    [Display(Name = "Einsatzende")] Einsatzende,
    /// <summary>Sonstiger Eintrag.</summary>
    [Display(Name = "Sonstiges")] Sonstiges,
}

/// <summary>
/// Zeitgestempelter Eintrag im Einsatztagebuch (V14). Auch live am Handy während des Einsatzes führbar (Andi, 25.10.2026); nach der
/// Freigabe des Berichts schreibgeschützt. Kein Führungsmittel und keine Alarmierung.
/// </summary>
public class Tagebucheintrag : Entitaet
{
    /// <summary>Kennung des Einsatzes.</summary>
    public Guid EinsatzId { get; set; }
    /// <summary>Navigationsverweis auf den Einsatz.</summary>
    public Einsatz? Einsatz { get; set; }

    /// <summary>Zeitpunkt des Ereignisses (nicht der Erfassung).</summary>
    public DateTimeOffset Zeitpunkt { get; set; }

    /// <summary>Art des Eintrags.</summary>
    public TagebuchArt Art { get; set; }

    /// <summary>Inhalt, z. B. „HLF 10 an Einsatzstelle, erkunde“.</summary>
    public required string Text { get; set; }

    /// <summary>Name des Verfassers zum Zeitpunkt der Erfassung.</summary>
    public string? Verfasser { get; set; }

    /// <summary>Benutzer, der den Eintrag angelegt hat.</summary>
    public Guid? VerfasstVon { get; set; }
}

/// <summary>Funktion bei einer Einsatz- oder Übungsteilnahme (V14), z. B. Gruppenführer oder Angriffstrupp, mit nötiger Qualifikation.</summary>
public class Einsatzfunktion : Katalogeintrag
{
    /// <summary>Nötige Qualifikationen als Codes, Alternativen mit „|“, z. B. „GF|ZF“; leer = keine.</summary>
    public string? Qualifikationen { get; set; }

    /// <summary>Gilt die Funktion als Atemschutzeinsatz (Angriffstrupp, Sicherheitstrupp); setzt im Bericht das Kennzeichen AGT vor.</summary>
    public bool Atemschutz { get; set; }

    /// <summary>Passt ein Freitext zur Funktion, nach Code oder Bezeichnung, ohne Rücksicht auf Groß- und Kleinschreibung.</summary>
    /// <param name="text">Eingetragene Funktion, z. B. „GF“ oder „Gruppenführer“.</param>
    /// <returns><c>true</c>, wenn der Text die Funktion bezeichnet.</returns>
    public bool Passt(string? text) =>
        !string.IsNullOrWhiteSpace(text)
        && (string.Equals(text.Trim(), Code, StringComparison.OrdinalIgnoreCase) || string.Equals(text.Trim(), Bezeichnung, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Prüft, ob die Person eine der nötigen Qualifikationen hat; gibt sonst einen Warnhinweis zurück. Nie blockierend: im Einsatz entscheidet
    /// der Einsatzleiter.
    /// </summary>
    /// <param name="vorhanden">Codes der gültigen Qualifikationen der Person.</param>
    /// <returns>Der Hinweis oder <c>null</c>, wenn alles passt.</returns>
    public string? Warnung(IReadOnlyCollection<string> vorhanden)
    {
        var noetig = (Qualifikationen ?? "").Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return noetig.Length == 0 || noetig.Any(vorhanden.Contains) ? null : $"{Bezeichnung}: Qualifikation {string.Join(" oder ", noetig)} fehlt.";
    }
}

/// <summary>Regeln der Einsatzleitung (V14).</summary>
public static class Einsatzfuehrung
{
    /// <summary>Einsatzleiter zu einem Zeitpunkt: der letzte Eintrag, der nicht nach dem Zeitpunkt liegt.</summary>
    /// <param name="verlauf">Übergaben.</param>
    /// <param name="zeitpunkt">Zeitpunkt.</param>
    /// <returns>Der Eintrag oder <c>null</c>.</returns>
    public static Einsatzleitung? Zum(IEnumerable<Einsatzleitung> verlauf, DateTimeOffset zeitpunkt) =>
        verlauf.Where(l => l.Ab <= zeitpunkt).OrderBy(l => l.Ab).LastOrDefault();

    /// <summary>Erster Einsatzleiter aus der eigenen Feuerwehr; wird in <see cref="Einsatz.EinsatzleiterId"/> für Statistik und Erfahrungsnachweis übernommen.</summary>
    /// <param name="verlauf">Übergaben.</param>
    /// <returns>Kennung des Mitglieds oder <c>null</c>.</returns>
    public static Guid? ErsterEigener(IEnumerable<Einsatzleitung> verlauf) =>
        verlauf.OrderBy(l => l.Ab).FirstOrDefault(l => l.MitgliedId is not null)?.MitgliedId;
}
