using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Laufbahn;

public enum EhrungGrundlage
{
    /// <summary>Anrechenbare Zeit im Einsatzdienst (Feuerwehr-Ehrenzeichen des Landes).</summary>
    [Display(Name = "Jahre im Einsatzdienst")] Einsatzdienst,

    /// <summary>Zugehörigkeit zur eigenen Feuerwehr in allen Abteilungen (Vereinsjubiläum).</summary>
    [Display(Name = "Jahre Zugehörigkeit")] Zugehoerigkeit,

    /// <summary>Ohne Jahresvoraussetzung, nur von Hand (Sonderstufe, Verbandsehrungen).</summary>
    [Display(Name = "nur von Hand")] Ohne,
}

public enum EhrungStatus
{
    [Display(Name = "vorgeschlagen")] Vorgeschlagen,
    [Display(Name = "beantragt")] Beantragt,
    [Display(Name = "verliehen")] Verliehen,
    [Display(Name = "abgelehnt")] Abgelehnt,
    [Display(Name = "verzichtet")] Verzichtet,
}

/// <summary>Katalog der Ehrungen (Ehrenzeichen BW, Vereinsjubiläen, Verbandsehrungen).</summary>
public class Ehrungsart : Katalogeintrag
{
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
    public int VorlaufMonate { get; set; } = 6;
}

public class Ehrung : Entitaet
{
    public Guid MitgliedId { get; set; }
    public Mitglied? Mitglied { get; set; }

    public Guid EhrungsartId { get; set; }
    public Ehrungsart? Ehrungsart { get; set; }

    public EhrungStatus Status { get; set; }

    /// <summary>Tag, an dem die Voraussetzung erfüllt war (aus der Dienstzeit berechnet oder von Hand).</summary>
    public DateOnly? VoraussetzungAm { get; set; }
    public DateOnly? BeantragtAm { get; set; }
    public DateOnly? VerliehenAm { get; set; }
    public string? Urkundennummer { get; set; }
    public string? Bemerkung { get; set; }
}

/// <summary>Fällige Ehrung, für die noch kein Datensatz existiert.</summary>
public sealed record Ehrungsvorschlag(Guid EhrungsartId, string Ehrungsart, int Jahre, DateOnly ErreichtAm);

public static class Ehrungsvorschlaege
{
    /// <summary>
    /// Je Reihe nur die höchste bis <c>stichtag + Vorlauf</c> erreichte Stufe, und nur, wenn weder sie noch eine höhere
    /// Stufe derselben Reihe schon erfasst ist (gleich mit welchem Status).
    /// </summary>
    public static IReadOnlyList<Ehrungsvorschlag> Ermitteln(
        IEnumerable<Ehrungsart> arten,
        IReadOnlyCollection<Guid> erfassteArten,
        Dienstzeit einsatzdienst,
        Dienstzeit zugehoerigkeit,
        DateOnly stichtag)
    {
        var aktiv = arten.Where(a => a.Aktiv && a.Grundlage != EhrungGrundlage.Ohne && a.Jahre > 0).ToList();
        var erfasst = aktiv.Where(a => erfassteArten.Contains(a.Id)).ToList();

        var kandidaten = aktiv
            .Select(a => (Art: a, Am: (a.Grundlage == EhrungGrundlage.Einsatzdienst ? einsatzdienst : zugehoerigkeit).ErreichtAm(a.Jahre!.Value)))
            .Where(k => k.Am is not null && k.Am <= stichtag.AddMonths(k.Art.VorlaufMonate))
            .Where(k => !erfasst.Any(e => e.Id == k.Art.Id
                || (k.Art.Reihe is not null && e.Reihe == k.Art.Reihe && e.Jahre >= k.Art.Jahre)));

        return kandidaten
            .GroupBy(k => k.Art.Reihe ?? k.Art.Code)
            .Select(g => g.MaxBy(k => k.Art.Jahre)!)
            .Select(k => new Ehrungsvorschlag(k.Art.Id, k.Art.Bezeichnung, k.Art.Jahre!.Value, k.Am!.Value))
            .OrderBy(v => v.ErreichtAm)
            .ToList();
    }
}
