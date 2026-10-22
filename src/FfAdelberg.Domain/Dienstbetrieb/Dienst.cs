using System.ComponentModel.DataAnnotations;
using FfAdelberg.Domain.Gemeinsam;
using FfAdelberg.Domain.Mitglieder;

namespace FfAdelberg.Domain.Dienstbetrieb;

public enum DienstArt
{
    [Display(Name = "Übung")] Uebung,
    [Display(Name = "Unterricht")] Unterricht,
    [Display(Name = "Sonderdienst")] Sonderdienst,
    [Display(Name = "Arbeitsdienst")] Arbeitsdienst,
    [Display(Name = "Sitzung")] Sitzung,
    [Display(Name = "Hauptversammlung")] Hauptversammlung,
    [Display(Name = "Veranstaltung")] Veranstaltung,
    [Display(Name = "Lehrgang (extern)")] Lehrgang,
}

public enum DienstStatus
{
    [Display(Name = "geplant")] Geplant,
    [Display(Name = "durchgeführt")] Durchgefuehrt,
    [Display(Name = "abgesagt")] Abgesagt,
}

/// <summary>Übung, Unterricht, Sitzung oder sonstiger Dienst mit Anwesenheitsliste.</summary>
public class Dienst : Entitaet
{
    public DienstArt Art { get; set; }
    public required string Titel { get; set; }
    public string? Thema { get; set; }
    public DateTimeOffset Beginn { get; set; }
    public DateTimeOffset Ende { get; set; }
    public string? Ort { get; set; }

    /// <summary>Zielgruppe; leer bedeutet alle Abteilungen.</summary>
    public Guid? AbteilungId { get; set; }
    public Abteilung? Abteilung { get; set; }

    public Guid? VerantwortlicherId { get; set; }
    public Mitglied? Verantwortlicher { get; set; }

    public DienstStatus Status { get; set; }

    /// <summary>Zählt für die jährliche Standortfortbildung (40 Stunden).</summary>
    public bool ZaehltFuerFortbildung { get; set; } = true;
    public string? Bericht { get; set; }

    public List<Anwesenheit> Anwesenheiten { get; set; } = [];

    public TimeSpan Dauer => Ende - Beginn;
}

public enum AnwesenheitStatus
{
    Anwesend,
    Entschuldigt,
    Unentschuldigt,
}

public enum ErfassungsWeg
{
    Web,
    Qr,
    Notbetrieb,
}

public class Anwesenheit : Entitaet
{
    public Guid DienstId { get; set; }
    public Dienst? Dienst { get; set; }

    public Guid MitgliedId { get; set; }
    public Mitglied? Mitglied { get; set; }

    public AnwesenheitStatus Status { get; set; }

    /// <summary>Leer bedeutet ganze Dienstdauer.</summary>
    public DateTimeOffset? Von { get; set; }
    public DateTimeOffset? Bis { get; set; }
    public string? Funktion { get; set; }
    public ErfassungsWeg ErfasstUeber { get; set; }
    public string? Bemerkung { get; set; }

    public TimeSpan Stunden(Dienst dienst) => Status == AnwesenheitStatus.Anwesend
        ? (Bis ?? dienst.Ende) - (Von ?? dienst.Beginn)
        : TimeSpan.Zero;
}
