using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Dokumente;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Gremien;

public enum SitzungArt
{
    [Display(Name = "Feuerwehrausschuss")] Ausschuss,
    [Display(Name = "Hauptversammlung")] Hauptversammlung,
    [Display(Name = "Abteilungsversammlung")] Abteilungsversammlung,
    [Display(Name = "Jugendfeuerwehrausschuss")] Jugendausschuss,
    [Display(Name = "Sonstige Besprechung")] Sonstige,
}

public enum SitzungStatus
{
    [Display(Name = "geplant")] Geplant,
    [Display(Name = "durchgeführt")] Durchgefuehrt,
    [Display(Name = "Protokoll genehmigt")] ProtokollGenehmigt,
    [Display(Name = "abgesagt")] Abgesagt,
}

/// <summary>Sitzung des Feuerwehrausschusses, Hauptversammlung oder andere Versammlung mit Niederschrift.</summary>
public class Sitzung : Entitaet
{
    public SitzungArt Art { get; set; }
    public required string Titel { get; set; }
    public DateTimeOffset Beginn { get; set; }
    public DateTimeOffset? Ende { get; set; }
    public string? Ort { get; set; }

    /// <summary>Tag, an dem die Einladung mit Tagesordnung verschickt wurde; für die Einladungsfrist.</summary>
    public DateOnly? EingeladenAm { get; set; }

    public string? Tagesordnung { get; set; }

    /// <summary>Niederschrift als Text. Ein unterschriebener Scan kann zusätzlich als Dokument hängen.</summary>
    public string? Protokoll { get; set; }

    public Guid? ProtokollfuehrerId { get; set; }
    public Mitglied? Protokollfuehrer { get; set; }

    public Guid? ProtokollDokumentId { get; set; }
    public Dokument? ProtokollDokument { get; set; }

    public SitzungStatus Status { get; set; }
    public DateOnly? ProtokollGenehmigtAm { get; set; }

    /// <summary>Wann die Niederschrift dem Bürgermeister vorgelegt wurde (bei Wahlen binnen einer Woche).</summary>
    public DateOnly? DemBuergermeisterVorgelegtAm { get; set; }

    public List<SitzungTeilnahme> Teilnahmen { get; set; } = [];
    public List<Beschluss> Beschluesse { get; set; } = [];
    public List<Wahl> Wahlen { get; set; } = [];

    /// <summary>
    /// Ob zwischen Einladung und Sitzungstag mindestens <paramref name="fristTage"/> Tage lagen.
    /// Null, solange kein Einladungsdatum erfasst ist.
    /// </summary>
    public static bool? EinladungsfristEingehalten(DateOnly? eingeladenAm, DateOnly sitzungstag, int fristTage) =>
        eingeladenAm is null ? null : sitzungstag.DayNumber - eingeladenAm.Value.DayNumber >= fristTage;

    /// <summary>Beschlussfähig, wenn mindestens die Hälfte der Stimmberechtigten anwesend ist.</summary>
    public static bool Beschlussfaehig(int anwesend, int stimmberechtigt) =>
        stimmberechtigt > 0 && anwesend * 2 >= stimmberechtigt;
}

public enum TeilnahmeStatus
{
    [Display(Name = "anwesend")] Anwesend,
    [Display(Name = "entschuldigt")] Entschuldigt,
    [Display(Name = "unentschuldigt")] Unentschuldigt,
}

public class SitzungTeilnahme : Entitaet
{
    public Guid SitzungId { get; set; }
    public Sitzung? Sitzung { get; set; }

    public Guid MitgliedId { get; set; }
    public Mitglied? Mitglied { get; set; }

    public TeilnahmeStatus Status { get; set; }

    /// <summary>Stimmberechtigt in dieser Sitzung (Ausschussmitglied, Angehöriger der Einsatzabteilung bei der Wahl).</summary>
    public bool Stimmberechtigt { get; set; } = true;
}

public enum BeschlussErgebnis
{
    [Display(Name = "angenommen")] Angenommen,
    [Display(Name = "abgelehnt")] Abgelehnt,
    [Display(Name = "vertagt")] Vertagt,
    [Display(Name = "zur Kenntnis genommen")] Kenntnisnahme,
}

/// <summary>Beschluss mit laufender Nummer je Jahr, z. B. 2026/07.</summary>
public class Beschluss : Entitaet
{
    public Guid SitzungId { get; set; }
    public Sitzung? Sitzung { get; set; }

    public int Jahr { get; set; }
    public int Nummer { get; set; }

    public string? Tagesordnungspunkt { get; set; }
    public required string Gegenstand { get; set; }

    public BeschlussErgebnis Ergebnis { get; set; }
    public int? JaStimmen { get; set; }
    public int? NeinStimmen { get; set; }
    public int? Enthaltungen { get; set; }

    /// <summary>Wiedervorlage: bis wann umzusetzen.</summary>
    public DateOnly? UmsetzenBis { get; set; }
    public DateOnly? ErledigtAm { get; set; }
    public string? Bemerkung { get; set; }

    public string Kennung => $"{Jahr}/{Nummer:00}";

    /// <summary>Einfache Mehrheit; bei Stimmengleichheit gilt ein Antrag als abgelehnt.</summary>
    public static BeschlussErgebnis AusStimmen(int ja, int nein) =>
        ja > nein ? BeschlussErgebnis.Angenommen : BeschlussErgebnis.Abgelehnt;
}
