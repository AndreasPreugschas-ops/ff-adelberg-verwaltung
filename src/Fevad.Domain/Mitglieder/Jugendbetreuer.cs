using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Mitglieder;

/// <summary>
/// Eintrag im Betreuer-Register der Jugend- oder Kinderfeuerwehr (V47): Feuerwehrangehörige und Helfer ohne Mitgliedschaft, z. B. Eltern bei
/// einer Fahrt. Vom erweiterten Führungszeugnis speichert FEVAD nur, was § 72a Abs. 5 SGB VIII erlaubt: dass es eingesehen wurde, wann und
/// von wem, das Datum des Zeugnisses und ob eine einschlägige Eintragung vorliegt. Löschung drei Monate nach dem Ende der Tätigkeit, ohne
/// Aufnahme der Tätigkeit sofort (L53).
/// </summary>
public class Jugendbetreuer : Entitaet
{
    /// <summary>Vorname.</summary>
    public required string Vorname { get; set; }
    /// <summary>Nachname.</summary>
    public required string Nachname { get; set; }
    /// <summary>Angehöriger der Feuerwehr; sonst Helfer ohne Mitgliedschaft.</summary>
    public bool Feuerwehrangehoeriger { get; set; }
    /// <summary>Jugend- oder Kinderfeuerwehr, in der die Person tätig ist.</summary>
    public Guid AbteilungId { get; set; }
    /// <summary>Abteilung.</summary>
    public Abteilung? Abteilung { get; set; }
    /// <summary>Aufgabe, z. B. „Betreuer“, „Eltern-Helfer Zeltlager“.</summary>
    public required string Aufgabe { get; set; }
    /// <summary>Beginn der Tätigkeit.</summary>
    public DateOnly Von { get; set; }
    /// <summary>Ende der Tätigkeit; leer, solange sie dauert. Liegt es vor dem Beginn, wurde die Tätigkeit nicht aufgenommen.</summary>
    public DateOnly? Bis { get; set; }

    /// <summary>
    /// Für die Tätigkeit ist ein erweitertes Führungszeugnis nötig. Nur nach Art, Intensität und Dauer des Kontakts verzichtbar
    /// (§ 72a Abs. 4 SGB VIII, Vereinbarung mit dem Jugendamt); dann ist der Grund anzugeben.
    /// </summary>
    public bool ZeugnisNoetig { get; set; } = true;
    /// <summary>Warum kein Führungszeugnis nötig ist.</summary>
    public string? ZeugnisNichtNoetigGrund { get; set; }
    /// <summary>Datum des Führungszeugnisses (Ausstellungsdatum).</summary>
    public DateOnly? ZeugnisVom { get; set; }
    /// <summary>Tag der Einsichtnahme.</summary>
    public DateOnly? EingesehenAm { get; set; }
    /// <summary>Wer eingesehen hat.</summary>
    public string? EingesehenVon { get; set; }
    /// <summary>Ergebnis: keine einschlägige Eintragung (§ 72a Abs. 1 SGB VIII); false heißt, die Person darf nicht eingesetzt werden.</summary>
    public bool? OhneEintrag { get; set; }

    /// <summary>Jugendleiter-Card gültig bis.</summary>
    public DateOnly? JuleicaBis { get; set; }
    /// <summary>Letzte Erste-Hilfe-Ausbildung.</summary>
    public DateOnly? ErsteHilfeAm { get; set; }
    /// <summary>Verhaltenskodex des Schutzkonzepts unterschrieben am.</summary>
    public DateOnly? VerhaltenskodexAm { get; set; }

    /// <summary>Anzeigename „Nachname, Vorname“.</summary>
    public string Anzeigename => $"{Nachname}, {Vorname}";

    /// <summary>Am Stichtag tätig.</summary>
    /// <param name="tag">Stichtag.</param>
    /// <returns><c>true</c>, wenn die Tätigkeit läuft.</returns>
    public bool TaetigAm(DateOnly tag) => Von <= tag && (Bis is null || Bis >= tag);
}

/// <summary>Stand des Führungszeugnisses eines Betreuers.</summary>
public enum Zeugnisstatus
{
    /// <summary>Kein Zeugnis nötig (mit Grund).</summary>
    [Display(Name = "nicht nötig")] NichtNoetig,
    /// <summary>Noch nicht eingesehen.</summary>
    [Display(Name = "fehlt")] Fehlt,
    /// <summary>Eingesehen und gültig.</summary>
    [Display(Name = "gültig")] Gueltig,
    /// <summary>Die Wiedervorlage steht bald an.</summary>
    [Display(Name = "bald erneuern")] Bald,
    /// <summary>Die Wiedervorlage ist überschritten.</summary>
    [Display(Name = "überfällig")] Faellig,
    /// <summary>Einschlägige Eintragung: nicht einsetzen.</summary>
    [Display(Name = "nicht einsetzen")] Eintrag,
}

/// <summary>Regeln zum erweiterten Führungszeugnis (V47); Fristen kommen aus den Parametern.</summary>
public static class Fuehrungszeugnisregeln
{
    /// <summary>Tag, bis zu dem das Zeugnis erneut vorzulegen ist: Einsichtnahme plus Wiedervorlage in Jahren.</summary>
    /// <param name="b">Der Betreuer.</param>
    /// <param name="jahre">Wiedervorlage in Jahren (Jugendfeuerwehr fünf, Kinderfeuerwehr zwei).</param>
    /// <returns>Der Tag oder null ohne Einsicht.</returns>
    public static DateOnly? NaechsteVorlage(Jugendbetreuer b, int jahre) => b.EingesehenAm?.AddYears(jahre);

    /// <summary>Stand des Zeugnisses am Stichtag.</summary>
    /// <param name="b">Der Betreuer.</param>
    /// <param name="heute">Stichtag.</param>
    /// <param name="jahre">Wiedervorlage in Jahren.</param>
    /// <param name="vorlaufTage">So viele Tage vorher gilt es als „bald erneuern“.</param>
    /// <returns>Der Stand.</returns>
    public static Zeugnisstatus Status(Jugendbetreuer b, DateOnly heute, int jahre, int vorlaufTage) =>
        !b.ZeugnisNoetig ? Zeugnisstatus.NichtNoetig
        : b.OhneEintrag == false ? Zeugnisstatus.Eintrag
        : NaechsteVorlage(b, jahre) is not { } faellig ? Zeugnisstatus.Fehlt
        : faellig <= heute ? Zeugnisstatus.Faellig
        : faellig.AddDays(-vorlaufTage) <= heute ? Zeugnisstatus.Bald
        : Zeugnisstatus.Gueltig;

    /// <summary>Das Zeugnis war bei der Einsicht älter als erlaubt (in der Regel drei Monate).</summary>
    /// <param name="zeugnisVom">Datum des Zeugnisses.</param>
    /// <param name="eingesehenAm">Tag der Einsichtnahme.</param>
    /// <param name="hoechstMonate">Höchstalter in Monaten.</param>
    /// <returns><c>true</c>, wenn es zu alt war.</returns>
    public static bool ZuAlt(DateOnly zeugnisVom, DateOnly eingesehenAm, int hoechstMonate) => zeugnisVom.AddMonths(hoechstMonate) < eingesehenAm;

    /// <summary>Darf eingesetzt werden: kein Zeugnis nötig oder gültig bzw. bald zu erneuern.</summary>
    /// <param name="status">Stand des Zeugnisses.</param>
    /// <returns><c>true</c>, wenn die Person eingesetzt werden darf.</returns>
    public static bool Einsetzbar(Zeugnisstatus status) => status is Zeugnisstatus.NichtNoetig or Zeugnisstatus.Gueltig or Zeugnisstatus.Bald;
}
