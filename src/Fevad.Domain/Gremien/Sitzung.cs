using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Dokumente;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Gremien;

/// <summary>Art einer Sitzung oder Versammlung.</summary>
public enum SitzungArt
{
    /// <summary>Sitzung des Feuerwehrausschusses.</summary>
    [Display(Name = "Feuerwehrausschuss")] Ausschuss,
    /// <summary>Hauptversammlung der Gemeindefeuerwehr.</summary>
    [Display(Name = "Hauptversammlung")] Hauptversammlung,
    /// <summary>Versammlung einer Abteilung.</summary>
    [Display(Name = "Abteilungsversammlung")] Abteilungsversammlung,
    /// <summary>Sitzung des Jugendfeuerwehrausschusses.</summary>
    [Display(Name = "Jugendfeuerwehrausschuss")] Jugendausschuss,
    /// <summary>Sonstige Besprechung mit Niederschrift.</summary>
    [Display(Name = "Sonstige Besprechung")] Sonstige,
}

/// <summary>Stand einer Sitzung.</summary>
public enum SitzungStatus
{
    /// <summary>Geplant, Einladung ggf. verschickt.</summary>
    [Display(Name = "geplant")] Geplant,
    /// <summary>Durchgeführt; die Niederschrift ist noch nicht genehmigt.</summary>
    [Display(Name = "durchgeführt")] Durchgefuehrt,
    /// <summary>Die Niederschrift ist genehmigt.</summary>
    [Display(Name = "Protokoll genehmigt")] ProtokollGenehmigt,
    /// <summary>Abgesagt.</summary>
    [Display(Name = "abgesagt")] Abgesagt,
}

/// <summary>Sitzung des Feuerwehrausschusses, Hauptversammlung oder andere Versammlung mit Niederschrift.</summary>
public class Sitzung : Entitaet
{
    /// <summary>Art der Sitzung; bestimmt die Einladungsfrist.</summary>
    public SitzungArt Art { get; set; }
    /// <summary>Titel, z. B. „Hauptversammlung 2027“.</summary>
    public required string Titel { get; set; }
    /// <summary>Beginn in UTC.</summary>
    public DateTimeOffset Beginn { get; set; }
    /// <summary>Ende in UTC.</summary>
    public DateTimeOffset? Ende { get; set; }
    /// <summary>Ort der Sitzung.</summary>
    public string? Ort { get; set; }

    /// <summary>Tag, an dem die Einladung mit Tagesordnung verschickt wurde; für die Einladungsfrist.</summary>
    public DateOnly? EingeladenAm { get; set; }

    /// <summary>Zeitpunkt, zu dem FEVAD die Einladung per E-Mail verschickt hat (Nachweis, V55).</summary>
    public DateTimeOffset? EinladungVersandtAm { get; set; }
    /// <summary>Zahl der Empfänger, die die Einladung per E-Mail erhalten haben.</summary>
    public int? EinladungEmpfaenger { get; set; }
    /// <summary>Zahl der Eingeladenen ohne E-Mail-Adresse; sie brauchen die Einladung auf Papier.</summary>
    public int? EinladungOhneMail { get; set; }
    /// <summary>Zeitpunkt, zu dem der Bürgermeister die Einladung mit Tagesordnung erhalten hat (Satzung § 13 Abs. 5, § 14 Abs. 3; F20).</summary>
    public DateTimeOffset? BuergermeisterEingeladenAm { get; set; }
    /// <summary>Dienst im Kalender, über den die Mitglieder zu- oder absagen (Hauptversammlung, V55).</summary>
    public Guid? DienstId { get; set; }

    /// <summary>Tagesordnung als Text.</summary>
    public string? Tagesordnung { get; set; }

    /// <summary>Niederschrift als Text. Ein unterschriebener Scan kann zusätzlich als Dokument hängen.</summary>
    public string? Protokoll { get; set; }

    /// <summary>Schlüssel des Protokollführers.</summary>
    public Guid? ProtokollfuehrerId { get; set; }
    /// <summary>Der Protokollführer, meist der Schriftführer.</summary>
    public Mitglied? Protokollfuehrer { get; set; }

    /// <summary>Schlüssel des unterschriebenen Protokoll-Scans in der Dokumentenablage.</summary>
    public Guid? ProtokollDokumentId { get; set; }
    /// <summary>Der unterschriebene Protokoll-Scan.</summary>
    public Dokument? ProtokollDokument { get; set; }

    /// <summary>Stand der Sitzung.</summary>
    public SitzungStatus Status { get; set; }
    /// <summary>Tag, an dem die Niederschrift genehmigt wurde, meist in der folgenden Sitzung.</summary>
    public DateOnly? ProtokollGenehmigtAm { get; set; }

    /// <summary>Wann die Niederschrift dem Bürgermeister vorgelegt wurde (bei Wahlen binnen einer Woche).</summary>
    public DateOnly? DemBuergermeisterVorgelegtAm { get; set; }

    /// <summary>Teilnehmer mit Anwesenheit und Stimmrecht.</summary>
    public List<SitzungTeilnahme> Teilnahmen { get; set; } = [];
    /// <summary>Gefasste Beschlüsse.</summary>
    public List<Beschluss> Beschluesse { get; set; } = [];
    /// <summary>Wahlgänge in dieser Sitzung.</summary>
    public List<Wahl> Wahlen { get; set; } = [];

    /// <summary>
    /// Ob zwischen Einladung und Sitzungstag mindestens <paramref name="fristTage"/> Tage lagen.
    /// Null, solange kein Einladungsdatum erfasst ist.
    /// </summary>
    /// <param name="eingeladenAm">Tag der Einladung.</param>
    /// <param name="sitzungstag">Tag der Sitzung.</param>
    /// <param name="fristTage">Einladungsfrist aus der Satzung (Parameter).</param>
    /// <returns>true oder false, oder null ohne Einladungsdatum.</returns>
    public static bool? EinladungsfristEingehalten(DateOnly? eingeladenAm, DateOnly sitzungstag, int fristTage) =>
        eingeladenAm is null ? null : sitzungstag.DayNumber - eingeladenAm.Value.DayNumber >= fristTage;

    /// <summary>
    /// Ampel der Pflicht, die Wahlniederschrift dem Bürgermeister vorzulegen: rot nach Ablauf der Frist, gelb davor; <c>null</c>, wenn
    /// vorgelegt oder ohne Wahl.
    /// </summary>
    /// <param name="heute">Heutiges Datum.</param>
    /// <param name="sitzungstag">Tag der Sitzung.</param>
    /// <param name="fristTage">Frist in Tagen (Parameter, eine Woche).</param>
    /// <returns>Die Ampel oder <c>null</c>.</returns>
    public Fristampel? WahlniederschriftAmpel(DateOnly heute, DateOnly sitzungstag, int fristTage) =>
        Wahlen.Count == 0 || DemBuergermeisterVorgelegtAm is not null || Status is SitzungStatus.Geplant or SitzungStatus.Abgesagt ? null
        : heute.DayNumber - sitzungstag.DayNumber > fristTage ? Fristampel.Rot
        : Fristampel.Gelb;

    /// <summary>Beschlussfähig, wenn mindestens die Hälfte der Stimmberechtigten anwesend ist.</summary>
    /// <param name="anwesend">Anwesende Stimmberechtigte.</param>
    /// <param name="stimmberechtigt">Stimmberechtigte insgesamt.</param>
    /// <returns>true, wenn die Sitzung beschlussfähig ist.</returns>
    public static bool Beschlussfaehig(int anwesend, int stimmberechtigt) =>
        stimmberechtigt > 0 && anwesend * 2 >= stimmberechtigt;
}

/// <summary>Anwesenheit bei einer Sitzung.</summary>
public enum TeilnahmeStatus
{
    /// <summary>Anwesend.</summary>
    [Display(Name = "anwesend")] Anwesend,
    /// <summary>Entschuldigt gefehlt.</summary>
    [Display(Name = "entschuldigt")] Entschuldigt,
    /// <summary>Unentschuldigt gefehlt.</summary>
    [Display(Name = "unentschuldigt")] Unentschuldigt,
}

/// <summary>Teilnahme eines Mitglieds an einer Sitzung.</summary>
public class SitzungTeilnahme : Entitaet
{
    /// <summary>Schlüssel der Sitzung.</summary>
    public Guid SitzungId { get; set; }
    /// <summary>Die Sitzung.</summary>
    public Sitzung? Sitzung { get; set; }

    /// <summary>Schlüssel des Mitglieds.</summary>
    public Guid MitgliedId { get; set; }
    /// <summary>Das Mitglied.</summary>
    public Mitglied? Mitglied { get; set; }

    /// <summary>Anwesend, entschuldigt oder unentschuldigt.</summary>
    public TeilnahmeStatus Status { get; set; }

    /// <summary>Stimmberechtigt in dieser Sitzung (Ausschussmitglied, Angehöriger der Einsatzabteilung bei der Wahl).</summary>
    public bool Stimmberechtigt { get; set; } = true;
}

/// <summary>Ergebnis eines Beschlusses.</summary>
public enum BeschlussErgebnis
{
    /// <summary>Angenommen.</summary>
    [Display(Name = "angenommen")] Angenommen,
    /// <summary>Abgelehnt, auch bei Stimmengleichheit.</summary>
    [Display(Name = "abgelehnt")] Abgelehnt,
    /// <summary>Auf eine spätere Sitzung vertagt.</summary>
    [Display(Name = "vertagt")] Vertagt,
    /// <summary>Ohne Abstimmung zur Kenntnis genommen.</summary>
    [Display(Name = "zur Kenntnis genommen")] Kenntnisnahme,
}

/// <summary>Stand eines Beschlusses im Beschlussregister (V55).</summary>
public enum BeschlussStand
{
    /// <summary>Angenommen, noch nicht umgesetzt.</summary>
    [Display(Name = "gefasst")] Gefasst,
    /// <summary>Angenommen und umgesetzt.</summary>
    [Display(Name = "umgesetzt")] Umgesetzt,
    /// <summary>Durch einen späteren Beschluss aufgehoben.</summary>
    [Display(Name = "aufgehoben")] Aufgehoben,
    /// <summary>Abgelehnt, vertagt oder nur zur Kenntnis genommen.</summary>
    [Display(Name = "nicht gefasst")] NichtGefasst,
}

/// <summary>Beschluss mit laufender Nummer je Jahr, z. B. 2026/07.</summary>
public class Beschluss : Entitaet
{
    /// <summary>Schlüssel der Sitzung.</summary>
    public Guid SitzungId { get; set; }
    /// <summary>Die Sitzung.</summary>
    public Sitzung? Sitzung { get; set; }

    /// <summary>Jahr der Beschlussnummer.</summary>
    public int Jahr { get; set; }
    /// <summary>Laufende Nummer im Jahr.</summary>
    public int Nummer { get; set; }

    /// <summary>Tagesordnungspunkt, z. B. „TOP 4“.</summary>
    public string? Tagesordnungspunkt { get; set; }
    /// <summary>Wortlaut oder Gegenstand des Beschlusses.</summary>
    public required string Gegenstand { get; set; }

    /// <summary>Ergebnis der Abstimmung.</summary>
    public BeschlussErgebnis Ergebnis { get; set; }
    /// <summary>Ja-Stimmen.</summary>
    public int? JaStimmen { get; set; }
    /// <summary>Nein-Stimmen.</summary>
    public int? NeinStimmen { get; set; }
    /// <summary>Enthaltungen.</summary>
    public int? Enthaltungen { get; set; }

    /// <summary>Wiedervorlage: bis wann umzusetzen.</summary>
    public DateOnly? UmsetzenBis { get; set; }
    /// <summary>Tag, an dem der Beschluss umgesetzt wurde.</summary>
    public DateOnly? ErledigtAm { get; set; }
    /// <summary>Freitext.</summary>
    public string? Bemerkung { get; set; }

    /// <summary>Tag, an dem der Beschluss aufgehoben wurde (Beschlussregister, V55).</summary>
    public DateOnly? AufgehobenAm { get; set; }
    /// <summary>Kennung oder Beschreibung des aufhebenden Beschlusses, z. B. „2027/03“.</summary>
    public string? AufgehobenDurch { get; set; }

    /// <summary>Stand im Beschlussregister.</summary>
    public BeschlussStand Stand =>
        AufgehobenAm is not null ? BeschlussStand.Aufgehoben
        : Ergebnis != BeschlussErgebnis.Angenommen ? BeschlussStand.NichtGefasst
        : ErledigtAm is not null ? BeschlussStand.Umgesetzt
        : BeschlussStand.Gefasst;

    /// <summary>Kennung „Jahr/Nummer“, z. B. „2026/07“.</summary>
    public string Kennung => $"{Jahr}/{Nummer:00}";

    /// <summary>Einfache Mehrheit; bei Stimmengleichheit gilt ein Antrag als abgelehnt.</summary>
    /// <param name="ja">Ja-Stimmen.</param>
    /// <param name="nein">Nein-Stimmen.</param>
    /// <returns>Angenommen bei mehr Ja- als Nein-Stimmen, sonst abgelehnt.</returns>
    public static BeschlussErgebnis AusStimmen(int ja, int nein) =>
        ja > nein ? BeschlussErgebnis.Angenommen : BeschlussErgebnis.Abgelehnt;
}
