using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Mitglieder;

/// <summary>Status eines Mitglieds in einer Abteilung während einer Mitgliedschaft.</summary>
public enum MitgliedStatus
{
    /// <summary>Neues Mitglied der Einsatzabteilung in der Probezeit (Satzung § 3 Abs. 2).</summary>
    [Display(Name = "Probezeit")] Probezeit,
    /// <summary>Aktives Mitglied der Abteilung.</summary>
    [Display(Name = "aktiv")] Aktiv,
    /// <summary>Vom Dienst befreit, z. B. wegen Krankheit oder Kindererziehung; Grund in <see cref="Mitgliedschaft.Befreiungsgrund"/>.</summary>
    [Display(Name = "befreit")] Befreit,
    /// <summary>Aus der Abteilung ausgeschieden; Grund in <see cref="Mitgliedschaft.Austrittsgrund"/>.</summary>
    [Display(Name = "ausgeschieden")] Ausgeschieden,
}

/// <summary>Grund, aus dem eine Mitgliedschaft in einer Abteilung endet.</summary>
public enum Austrittsgrund
{
    /// <summary>Altersgrenze der Abteilung erreicht.</summary>
    [Display(Name = "Altersgrenze")] Altersgrenze,
    /// <summary>Probezeit nicht bestanden.</summary>
    [Display(Name = "Probezeit nicht bestanden")] ProbezeitNichtBestanden,
    /// <summary>Aus gesundheitlichen Gründen ausgeschieden; ohne weitere Gesundheitsangaben.</summary>
    [Display(Name = "gesundheitliche Gründe")] Gesundheit,
    /// <summary>Wohnort in eine andere Gemeinde verlegt.</summary>
    [Display(Name = "Wohnortwechsel")] Wohnortwechsel,
    /// <summary>Austritt auf eigenen Antrag.</summary>
    [Display(Name = "Austritt auf Antrag")] Austritt,
    /// <summary>Ausschluss aus der Feuerwehr.</summary>
    [Display(Name = "Ausschluss")] Ausschluss,
    /// <summary>Tod des Mitglieds.</summary>
    [Display(Name = "Tod")] Tod,
    /// <summary>Wechsel in eine andere Abteilung, z. B. Jugendfeuerwehr in die Einsatzabteilung.</summary>
    [Display(Name = "Übertritt")] Uebertritt,
}

/// <summary>Art einer Vordienstzeit; bestimmt die Anrechnung für das Feuerwehr-Ehrenzeichen (VwV Nr. 4).</summary>
public enum Vordienstart
{
    /// <summary>Dienst in einer anderen Freiwilligen Feuerwehr, auch außerhalb Baden-Württembergs.</summary>
    [Display(Name = "andere Freiwillige Feuerwehr (auch anderes Bundesland)")] AndereFeuerwehr,
    /// <summary>Dienst in einer Berufs- oder Werkfeuerwehr.</summary>
    [Display(Name = "Berufs- oder Werkfeuerwehr")] BerufsOderWerkfeuerwehr,
    /// <summary>Dienst in einer Feuerwehr im Ausland.</summary>
    [Display(Name = "ausländische Feuerwehr")] Ausland,
    /// <summary>Wehr- oder Zivildienst.</summary>
    [Display(Name = "Wehr- oder Zivildienst")] Wehrdienst,
    /// <summary>Bundesfreiwilligendienst, Freiwilliges Soziales oder Ökologisches Jahr.</summary>
    [Display(Name = "Bundesfreiwilligendienst, FSJ oder FÖJ")] Freiwilligendienst,
}

/// <summary>Grund einer Dienstbefreiung; nur Krankheit, Mutterschutz und (begrenzt) Kindererziehung zählen weiter.</summary>
public enum Befreiungsgrund
{
    /// <summary>Anderer Grund; die Zeit zählt nicht als Dienstzeit.</summary>
    [Display(Name = "sonstiger Grund")] Sonstiges,
    /// <summary>Kindererziehung; zählt begrenzt als Dienstzeit.</summary>
    [Display(Name = "Kindererziehung")] Kindererziehung,
    /// <summary>Krankheit; zählt als Dienstzeit.</summary>
    [Display(Name = "Krankheit")] Krankheit,
    /// <summary>Mutterschutz; zählt als Dienstzeit.</summary>
    [Display(Name = "Mutterschutz")] Mutterschutz,
}

/// <summary>
/// Zugehörigkeit zu einer Abteilung über einen Zeitraum. Wird nie überschrieben,
/// sondern beendet und neu angelegt (Dienstjahre, Personalstand zum Stichtag).
/// </summary>
public class Mitgliedschaft : Entitaet
{
    /// <summary>Schlüssel des Mitglieds.</summary>
    public Guid MitgliedId { get; set; }
    /// <summary>Das Mitglied, zu dem die Mitgliedschaft gehört.</summary>
    public Mitglied? Mitglied { get; set; }

    /// <summary>Schlüssel der Abteilung.</summary>
    public Guid AbteilungId { get; set; }
    /// <summary>Die Abteilung, in der das Mitglied in diesem Zeitraum war.</summary>
    public Abteilung? Abteilung { get; set; }

    /// <summary>Status in diesem Zeitraum, z. B. Probezeit oder befreit.</summary>
    public MitgliedStatus Status { get; set; }
    /// <summary>Erster Tag der Mitgliedschaft.</summary>
    public DateOnly GueltigAb { get; set; }
    /// <summary>Letzter Tag der Mitgliedschaft; leer, solange sie andauert.</summary>
    public DateOnly? GueltigBis { get; set; }
    /// <summary>Grund, aus dem die Mitgliedschaft endete; nur bei beendeten Mitgliedschaften.</summary>
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

    /// <summary>Freitext zur Mitgliedschaft.</summary>
    public string? Bemerkung { get; set; }

    /// <summary>
    /// Letzter Tag der Probezeit. Beim Eintritt mit der Probezeit aus der Satzung vorbelegt; der Feuerwehrausschuss kann
    /// sie verkürzen, verlängern oder darauf verzichten (Satzung § 3 Abs. 2). Bestanden ist sie erst mit der Bestätigung.
    /// </summary>
    public DateOnly? ProbezeitBis { get; set; }

    /// <summary>Grund oder Beschluss zu einer abweichenden Probezeit, z. B. „Verkürzt, Übertritt aus der JF, Ausschuss 12.03.2027“.</summary>
    public string? ProbezeitBemerkung { get; set; }

    /// <summary>Prüft, ob die Mitgliedschaft an einem Tag besteht.</summary>
    /// <param name="stichtag">Zu prüfender Tag.</param>
    /// <returns>true, wenn der Stichtag zwischen Beginn und Ende liegt (jeweils einschließlich).</returns>
    public bool IstAktivAm(DateOnly stichtag) =>
        GueltigAb <= stichtag && (GueltigBis is null || stichtag <= GueltigBis);

    /// <summary>Letzter Tag der Probezeit: das eingetragene Datum, sonst Beginn plus Probezeit aus der Satzung.</summary>
    /// <param name="probezeitMonate">Probezeit nach der Satzung in Monaten (Parameter <see cref="ParameterSchluessel.ProbezeitMonate"/>).</param>
    /// <returns>Den letzten Tag der Probezeit.</returns>
    public DateOnly ProbezeitEnde(int probezeitMonate) => ProbezeitBis ?? GueltigAb.AddMonths(probezeitMonate);
}
