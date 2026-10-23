using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Mitglieder;

public enum MitgliedStatus
{
    [Display(Name = "Probezeit")] Probezeit,
    [Display(Name = "aktiv")] Aktiv,
    [Display(Name = "befreit")] Befreit,
    [Display(Name = "ausgeschieden")] Ausgeschieden,
}

public enum Austrittsgrund
{
    [Display(Name = "Altersgrenze")] Altersgrenze,
    [Display(Name = "Probezeit nicht bestanden")] ProbezeitNichtBestanden,
    [Display(Name = "gesundheitliche Gründe")] Gesundheit,
    [Display(Name = "Wohnortwechsel")] Wohnortwechsel,
    [Display(Name = "Austritt auf Antrag")] Austritt,
    [Display(Name = "Ausschluss")] Ausschluss,
    [Display(Name = "Tod")] Tod,
    [Display(Name = "Übertritt")] Uebertritt,
}

/// <summary>Art einer Vordienstzeit; bestimmt die Anrechnung für das Feuerwehr-Ehrenzeichen (VwV Nr. 4).</summary>
public enum Vordienstart
{
    [Display(Name = "andere Freiwillige Feuerwehr (auch anderes Bundesland)")] AndereFeuerwehr,
    [Display(Name = "Berufs- oder Werkfeuerwehr")] BerufsOderWerkfeuerwehr,
    [Display(Name = "ausländische Feuerwehr")] Ausland,
    [Display(Name = "Wehr- oder Zivildienst")] Wehrdienst,
    [Display(Name = "Bundesfreiwilligendienst, FSJ oder FÖJ")] Freiwilligendienst,
}

/// <summary>Grund einer Dienstbefreiung; nur Krankheit, Mutterschutz und (begrenzt) Kindererziehung zählen weiter.</summary>
public enum Befreiungsgrund
{
    [Display(Name = "sonstiger Grund")] Sonstiges,
    [Display(Name = "Kindererziehung")] Kindererziehung,
    [Display(Name = "Krankheit")] Krankheit,
    [Display(Name = "Mutterschutz")] Mutterschutz,
}

/// <summary>
/// Zugehörigkeit zu einer Abteilung über einen Zeitraum. Wird nie überschrieben,
/// sondern beendet und neu angelegt (Dienstjahre, Personalstand zum Stichtag).
/// </summary>
public class Mitgliedschaft : Entitaet
{
    public Guid MitgliedId { get; set; }
    public Mitglied? Mitglied { get; set; }

    public Guid AbteilungId { get; set; }
    public Abteilung? Abteilung { get; set; }

    public MitgliedStatus Status { get; set; }
    public DateOnly GueltigAb { get; set; }
    public DateOnly? GueltigBis { get; set; }
    public Austrittsgrund? Austrittsgrund { get; set; }

    /// <summary>Zeiten in anderen Wehren, für das Ehrenzeichen anrechenbar.</summary>
    public bool Vordienstzeit { get; set; }

    /// <summary>Nur bei Vordienstzeiten; leer heißt andere Feuerwehr.</summary>
    public Vordienstart? Vordienstart { get; set; }

    /// <summary>Nur bei Status <see cref="MitgliedStatus.Befreit"/>.</summary>
    public Befreiungsgrund? Befreiungsgrund { get; set; }

    /// <summary>
    /// Vom Kommandanten bestätigt, dass die Zeit als Einsatzdienst zählt: in der Altersabteilung Aufgaben nach der
    /// Konzeption 65plus, in der Musikabteilung Grundausbildung, Übungsdienst und Einsatzbereitschaft.
    /// </summary>
    public bool EinsatzdienstBestaetigt { get; set; }

    public string? Bemerkung { get; set; }

    /// <summary>
    /// Letzter Tag der Probezeit. Beim Eintritt mit der Probezeit aus der Satzung vorbelegt; der Feuerwehrausschuss kann
    /// sie verkürzen, verlängern oder darauf verzichten (Satzung § 3 Abs. 2). Bestanden ist sie erst mit der Bestätigung.
    /// </summary>
    public DateOnly? ProbezeitBis { get; set; }

    /// <summary>Grund oder Beschluss zu einer abweichenden Probezeit, z. B. „Verkürzt, Übertritt aus der JF, Ausschuss 12.03.2027“.</summary>
    public string? ProbezeitBemerkung { get; set; }

    public bool IstAktivAm(DateOnly stichtag) =>
        GueltigAb <= stichtag && (GueltigBis is null || stichtag <= GueltigBis);

    /// <summary>Letzter Tag der Probezeit: das eingetragene Datum, sonst Beginn plus Probezeit aus der Satzung.</summary>
    public DateOnly ProbezeitEnde(int probezeitMonate) => ProbezeitBis ?? GueltigAb.AddMonths(probezeitMonate);
}
