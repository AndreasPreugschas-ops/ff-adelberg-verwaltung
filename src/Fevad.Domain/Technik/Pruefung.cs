using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Technik;

public enum PruefErgebnis
{
    [Display(Name = "bestanden")] Bestanden,
    [Display(Name = "bestanden mit Mängeln")] BestandenMitMaengeln,
    [Display(Name = "nicht bestanden")] NichtBestanden,
    [Display(Name = "ausgesondert")] Ausgesondert,
}

/// <summary>
/// Prüfprotokoll für ein Gerät oder ein Fahrzeug (genau eines von beiden).
/// Nach Abschluss unveränderlich; Korrekturen als neue Prüfung.
/// </summary>
public class Pruefung : Entitaet
{
    public Guid? GeraetId { get; set; }
    public Geraet? Geraet { get; set; }

    public Guid? FahrzeugId { get; set; }
    public Fahrzeug? Fahrzeug { get; set; }

    public Guid? PruefplanPositionId { get; set; }
    public PruefplanPosition? PruefplanPosition { get; set; }

    public Pruefart Pruefart { get; set; }
    public DateOnly Datum { get; set; }
    public PruefErgebnis Ergebnis { get; set; }

    /// <summary>Ausgefüllte Checkliste mit Messwerten als JSON.</summary>
    public string Ergebnisse { get; set; } = "[]";
    public DateOnly? NaechsteFaellig { get; set; }

    /// <summary>Befähigte Person nach DGUV 305-002.</summary>
    public Guid? PrueferId { get; set; }
    public Mitglied? Pruefer { get; set; }
    public string? PrueferExtern { get; set; }

    /// <summary>Prüfung nach Gebrauch im Einsatz.</summary>
    public Guid? EinsatzId { get; set; }

    public bool Abgeschlossen { get; set; }
    public bool ErfasstImNotbetrieb { get; set; }
    public string? Bemerkung { get; set; }

    public void NaechsteFaelligkeitVorbelegen(PruefplanPosition position)
    {
        NaechsteFaellig ??= Datum.AddMonths(position.IntervallMonate);
    }
}

public enum MangelSchwere
{
    [Display(Name = "Hinweis")] Info,
    [Display(Name = "Einschränkung")] Einschraenkung,
    [Display(Name = "außer Betrieb")] AusserBetrieb,
}

public enum MangelStatus
{
    [Display(Name = "offen")] Offen,
    [Display(Name = "in Arbeit")] InArbeit,
    [Display(Name = "behoben")] Behoben,
}

public class Mangel : Entitaet
{
    public Guid? GeraetId { get; set; }
    public Geraet? Geraet { get; set; }

    public Guid? FahrzeugId { get; set; }
    public Fahrzeug? Fahrzeug { get; set; }

    public Guid? GemeldetVonId { get; set; }
    public Mitglied? GemeldetVon { get; set; }
    public DateTimeOffset GemeldetAm { get; set; }

    public required string Beschreibung { get; set; }
    public MangelSchwere Schwere { get; set; }
    public MangelStatus Status { get; set; }
    public DateTimeOffset? BehobenAm { get; set; }

    /// <summary>Was der Gerätewart getan hat bzw. tun will.</summary>
    public string? Massnahme { get; set; }
    public Guid? PruefungId { get; set; }

    /// <summary>Ein Mangel mit Status Offen oder InArbeit schränkt das Gerät ein.</summary>
    public bool IstOffen => Status != MangelStatus.Behoben;
}
