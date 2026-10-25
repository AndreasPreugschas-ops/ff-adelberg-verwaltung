using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Laufbahn;

/// <summary>Woraus sich der Anspruch auf eine Ehrung ergibt.</summary>
public enum EhrungGrundlage
{
    /// <summary>Anrechenbare Zeit im Einsatzdienst (Feuerwehr-Ehrenzeichen des Landes).</summary>
    [Display(Name = "Jahre im Einsatzdienst")] Einsatzdienst,

    /// <summary>Zugehörigkeit zur eigenen Feuerwehr in allen Abteilungen (Vereinsjubiläum).</summary>
    [Display(Name = "Jahre Zugehörigkeit")] Zugehoerigkeit,

    /// <summary>Ohne Jahresvoraussetzung, nur von Hand (besondere Verdienste, Verbandsehrungen).</summary>
    [Display(Name = "nur von Hand")] Ohne,

    /// <summary>Jahre in einer Funktion, z. B. Jugendfeuerwehrwart (Ehrennadel der Jugendfeuerwehr).</summary>
    [Display(Name = "Jahre in Funktion")] Funktion,

    /// <summary>Jahre in der Jugendfeuerwehr der eigenen Wehr (Traditionsnadel).</summary>
    [Display(Name = "Jahre in der Jugendfeuerwehr")] Jugendfeuerwehr,
}

/// <summary>Bearbeitungsstand einer Ehrung.</summary>
public enum EhrungStatus
{
    /// <summary>Von FEVAD oder von Hand vorgeschlagen, noch nicht beantragt.</summary>
    [Display(Name = "vorgeschlagen")] Vorgeschlagen,
    /// <summary>Beim Verleiher beantragt.</summary>
    [Display(Name = "beantragt")] Beantragt,
    /// <summary>Verliehen.</summary>
    [Display(Name = "verliehen")] Verliehen,
    /// <summary>Vom Verleiher abgelehnt.</summary>
    [Display(Name = "abgelehnt")] Abgelehnt,
    /// <summary>Das Mitglied verzichtet; FEVAD schlägt die Ehrung nicht erneut vor.</summary>
    [Display(Name = "verzichtet")] Verzichtet,
}

/// <summary>Katalog der Ehrungen (Ehrenzeichen BW, Vereinsjubiläen, Verbandsehrungen).</summary>
public class Ehrungsart : Katalogeintrag
{
    /// <summary>Grundlage der Jahre.</summary>
    public EhrungGrundlage Grundlage { get; set; }

    /// <summary>Erforderliche Jahre; leer bei <see cref="EhrungGrundlage.Ohne"/>.</summary>
    public int? Jahre { get; set; }

    /// <summary>
    /// Stufen derselben Reihe ersetzen sich: Wer Silber hat, bekommt keinen Vorschlag mehr für Bronze.
    /// Zum Beispiel "FEZ" für das Feuerwehr-Ehrenzeichen.
    /// </summary>
    public string? Reihe { get; set; }

    /// <summary>Wer verleiht, z. B. "Landratsamt".</summary>
    public string? Verleiher { get; set; }

    /// <summary>Zeitlicher Vorlauf für den Vorschlag, z. B. wegen des Antrags über das Bürgermeisteramt.</summary>
    public int VorlaufWochen { get; set; } = 26;

    /// <summary>Bei <see cref="EhrungGrundlage.Funktion"/>: Code der Funktion, z. B. "JW".</summary>
    public string? FunktionCode { get; set; }

    /// <summary>Diese Ehrung setzt die verliehene Vorstufe voraus, z. B. DFV-Ehrenkreuz Gold nach Silber.</summary>
    public Guid? VorstufeId { get; set; }
    /// <summary>Die vorausgesetzte Ehrungsart.</summary>
    public Ehrungsart? Vorstufe { get; set; }

    /// <summary>Mindestabstand zur Verleihung der Vorstufe in Jahren.</summary>
    public int? MindestabstandJahre { get; set; }

    /// <summary>Kein Vorschlag, wenn eine dieser Qualifikationen vorliegt (Traditionsnadel: ohne Jugendflamme und Leistungsspange).</summary>
    public List<string> NichtMitQualifikationen { get; set; } = [];

    /// <summary>Weg des Antrags, z. B. "Kommandant → KFV → LFV → DFV".</summary>
    public string? Antragsweg { get; set; }

    /// <summary>Kontingent als Hinweis, z. B. "1 je 1.000 Angehörige im Jahr".</summary>
    public string? Kontingent { get; set; }

    /// <summary>Verbandsehrung mit gemeinsamem Antragsschluss (Parameter <c>Ehrung.Verband.Antragsschluss</c>).</summary>
    public bool MitAntragsschluss { get; set; }
}

/// <summary>Erfasste Ehrung eines Mitglieds vom Vorschlag bis zur Verleihung.</summary>
public class Ehrung : Entitaet
{
    /// <summary>Schlüssel des Mitglieds.</summary>
    public Guid MitgliedId { get; set; }
    /// <summary>Das Mitglied.</summary>
    public Mitglied? Mitglied { get; set; }

    /// <summary>Schlüssel der Ehrungsart.</summary>
    public Guid EhrungsartId { get; set; }
    /// <summary>Die Ehrungsart.</summary>
    public Ehrungsart? Ehrungsart { get; set; }

    /// <summary>Bearbeitungsstand.</summary>
    public EhrungStatus Status { get; set; }

    /// <summary>Tag, an dem die Voraussetzung erfüllt war (aus der Dienstzeit berechnet oder von Hand).</summary>
    public DateOnly? VoraussetzungAm { get; set; }
    /// <summary>Tag des Antrags beim Verleiher.</summary>
    public DateOnly? BeantragtAm { get; set; }
    /// <summary>Tag der Verleihung.</summary>
    public DateOnly? VerliehenAm { get; set; }
    /// <summary>Nummer der Urkunde.</summary>
    public string? Urkundennummer { get; set; }
    /// <summary>Freitext.</summary>
    public string? Bemerkung { get; set; }
}

/// <summary>Fällige Ehrung, für die noch kein Datensatz existiert. <paramref name="Jahre"/> ist 0 bei Ehrungen ohne Jahre.</summary>
/// <param name="EhrungsartId">Schlüssel der Ehrungsart.</param>
/// <param name="Ehrungsart">Bezeichnung der Ehrungsart.</param>
/// <param name="Jahre">Erforderliche Jahre.</param>
/// <param name="ErreichtAm">Tag, an dem die Voraussetzung erfüllt ist.</param>
public sealed record Ehrungsvorschlag(Guid EhrungsartId, string Ehrungsart, int Jahre, DateOnly ErreichtAm);

/// <summary>Schon erfasste Ehrung eines Mitglieds.</summary>
/// <param name="EhrungsartId">Schlüssel der Ehrungsart.</param>
/// <param name="Status">Bearbeitungsstand.</param>
/// <param name="VerliehenAm">Tag der Verleihung.</param>
public sealed record ErfassteEhrung(Guid EhrungsartId, EhrungStatus Status, DateOnly? VerliehenAm);

/// <summary>Zeiten und Qualifikationen eines Mitglieds, aus denen sich Ehrungen ergeben.</summary>
/// <param name="Einsatzdienst">Anrechenbare Einsatzdienstzeit nach VwV.</param>
/// <param name="Zugehoerigkeit">Zugehörigkeit zur Feuerwehr in allen Abteilungen.</param>
/// <param name="Jugendfeuerwehr">Zeit in der eigenen Jugendfeuerwehr.</param>
/// <param name="Funktionen">Zeit je Funktionscode.</param>
/// <param name="Qualifikationen">Codes der vorhandenen Qualifikationen.</param>
public sealed record Ehrungsgrundlagen(
    Dienstzeit Einsatzdienst,
    Dienstzeit Zugehoerigkeit,
    Dienstzeit Jugendfeuerwehr,
    IReadOnlyDictionary<string, Dienstzeit> Funktionen,
    IReadOnlySet<string> Qualifikationen);

/// <summary>Ermittelt fällige Ehrungen aus den Dienstzeiten eines Mitglieds.</summary>
public static class Ehrungsvorschlaege
{
    /// <summary>
    /// Je Reihe nur die höchste bis <c>stichtag + Vorlauf</c> erreichte Stufe, und nur, wenn weder sie noch eine höhere
    /// Stufe derselben Reihe schon erfasst ist (gleich mit welchem Status). Mit Vorstufe erst, wenn die Vorstufe
    /// verliehen ist und der Mindestabstand vergangen ist.
    /// </summary>
    /// <param name="arten">Alle Ehrungsarten; nur aktive mit Jahresvoraussetzung zählen.</param>
    /// <param name="erfasste">Schon erfasste Ehrungen des Mitglieds.</param>
    /// <param name="grundlagen">Zeiten und Qualifikationen des Mitglieds.</param>
    /// <param name="stichtag">Heutiger Tag.</param>
    /// <returns>Die Vorschläge, nach Tag des Erreichens sortiert.</returns>
    public static IReadOnlyList<Ehrungsvorschlag> Ermitteln(
        IEnumerable<Ehrungsart> arten,
        IReadOnlyCollection<ErfassteEhrung> erfasste,
        Ehrungsgrundlagen grundlagen,
        DateOnly stichtag)
    {
        var aktiv = arten.Where(a => a.Aktiv && a.Grundlage != EhrungGrundlage.Ohne && a.Jahre > 0).ToList();
        var erfassteIds = erfasste.Select(e => e.EhrungsartId).ToHashSet();
        var erfassteArten = aktiv.Where(a => erfassteIds.Contains(a.Id)).ToList();

        var kandidaten = aktiv
            .Where(a => !a.NichtMitQualifikationen.Any(grundlagen.Qualifikationen.Contains))
            .Select(a => (Art: a, Am: Erreicht(a, grundlagen, erfasste)))
            .Where(k => k.Am is not null && k.Am <= stichtag.AddDays(7 * k.Art.VorlaufWochen))
            .Where(k => !erfassteArten.Any(e => e.Id == k.Art.Id
                || (k.Art.Reihe is not null && e.Reihe == k.Art.Reihe && e.Jahre >= k.Art.Jahre)));

        return kandidaten
            .GroupBy(k => k.Art.Reihe ?? k.Art.Code)
            .Select(g => g.MaxBy(k => k.Art.Jahre)!)
            .Select(k => new Ehrungsvorschlag(k.Art.Id, k.Art.Bezeichnung, k.Art.Jahre!.Value, k.Am!.Value))
            .OrderBy(v => v.ErreichtAm)
            .ToList();
    }

    private static DateOnly? Erreicht(Ehrungsart art, Ehrungsgrundlagen g, IReadOnlyCollection<ErfassteEhrung> erfasste)
    {
        var zeit = art.Grundlage switch
        {
            EhrungGrundlage.Einsatzdienst => g.Einsatzdienst,
            EhrungGrundlage.Zugehoerigkeit => g.Zugehoerigkeit,
            EhrungGrundlage.Jugendfeuerwehr => g.Jugendfeuerwehr,
            EhrungGrundlage.Funktion when art.FunktionCode is not null => g.Funktionen.GetValueOrDefault(art.FunktionCode),
            _ => null,
        };
        var am = zeit?.ErreichtAm(art.Jahre!.Value);
        if (am is null || art.VorstufeId is null)
        {
            return am;
        }

        var vorstufe = erfasste.Where(e => e.EhrungsartId == art.VorstufeId && e.Status == EhrungStatus.Verliehen && e.VerliehenAm is not null)
            .Select(e => e.VerliehenAm!.Value).DefaultIfEmpty().Max();
        if (vorstufe == default)
        {
            return null;
        }

        var fruehestens = vorstufe.AddYears(art.MindestabstandJahre ?? 0);
        return fruehestens > am ? fruehestens : am;
    }

    /// <summary>
    /// Hinweis beim Erfassen, wenn die Vorstufe fehlt oder der Mindestabstand nicht eingehalten ist; sonst null.
    /// Gespeichert wird trotzdem (ältere Ehrungen sind oft nicht vollständig erfasst).
    /// </summary>
    /// <param name="art">Die zu erfassende Ehrungsart.</param>
    /// <param name="vorstufe">Die Vorstufe, wenn eine gefordert ist.</param>
    /// <param name="erfasste">Schon erfasste Ehrungen des Mitglieds.</param>
    /// <param name="am">Tag der Verleihung der neuen Ehrung.</param>
    /// <returns>Der Hinweistext oder null.</returns>
    public static string? VorstufeHinweis(Ehrungsart art, Ehrungsart? vorstufe, IReadOnlyCollection<ErfassteEhrung> erfasste, DateOnly am)
    {
        if (art.VorstufeId is null || vorstufe is null)
        {
            return null;
        }

        var verliehen = erfasste.Where(e => e.EhrungsartId == art.VorstufeId && e.Status == EhrungStatus.Verliehen)
            .Select(e => e.VerliehenAm).Max();
        if (verliehen is null)
        {
            return $"Voraussetzung ist {vorstufe.Bezeichnung}, die hier nicht als verliehen erfasst ist.";
        }

        var jahre = art.MindestabstandJahre ?? 0;
        if (verliehen.Value.AddYears(jahre) <= am)
        {
            return null;
        }

        return jahre == 0
            ? $"{vorstufe.Bezeichnung} wurde erst am {verliehen:dd.MM.yyyy} verliehen."
            : $"Seit {vorstufe.Bezeichnung} ({verliehen:dd.MM.yyyy}) sind noch keine {jahre} Jahre vergangen.";
    }
}

/// <summary>Antragsschluss für Verbandsehrungen, z. B. „30.11.“ für das Folgejahr.</summary>
public static class Antragsschluss
{
    /// <summary>Nächster Antragsschluss am oder nach dem Stichtag; null, wenn der Wert nicht als „TT.MM.“ lesbar ist.</summary>
    /// <param name="tagMonat">Antragsschluss als „TT.MM.“.</param>
    /// <param name="stichtag">Heutiger Tag.</param>
    /// <returns>Das Datum des nächsten Antragsschlusses oder null.</returns>
    public static DateOnly? Naechster(string? tagMonat, DateOnly stichtag)
    {
        var teile = (tagMonat ?? "").Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (teile.Length < 2 || !int.TryParse(teile[0], out var tag) || !int.TryParse(teile[1], out var monat)
            || monat is < 1 or > 12 || tag < 1 || tag > DateTime.DaysInMonth(stichtag.Year, monat))
        {
            return null;
        }

        var dieses = new DateOnly(stichtag.Year, monat, tag);
        return dieses >= stichtag ? dieses : dieses.AddYears(1);
    }
}
