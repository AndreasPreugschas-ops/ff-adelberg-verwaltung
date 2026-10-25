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
/// Nach Abschluss unveränderlich; Korrekturen durch Storno und neue Prüfung (V58, Signaturersatz bei EDV-Dokumentation).
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

    /// <summary>Stornierte Prüfungen bleiben im Prüfbuch sichtbar, zählen aber für keine Frist.</summary>
    public DateTimeOffset? StorniertAm { get; set; }

    /// <summary>Benutzerkonto, das storniert hat.</summary>
    public Guid? StorniertVon { get; set; }
    /// <summary>Pflichtangabe beim Storno, z. B. „falsches Gerät gescannt“.</summary>
    public string? Stornogrund { get; set; }

    /// <summary>Die Prüfung ist storniert und zählt für keine Frist.</summary>
    public bool Storniert => StorniertAm is not null;

    /// <summary>Gründe gegen ein Storno; leer heißt, das Storno ist möglich.</summary>
    public string? StornoHindernis() => this switch
    {
        { Storniert: true } => "Die Prüfung ist bereits storniert.",
        { Ergebnis: PruefErgebnis.Ausgesondert } => "Eine Aussonderung wird am Gerät zurückgenommen, nicht durch Storno.",
        _ => null,
    };

    /// <summary>Storniert die Prüfung; wirft, wenn ein Hindernis besteht oder der Grund fehlt.</summary>
    public void Stornieren(Guid benutzerId, string grund, DateTimeOffset jetzt)
    {
        if (StornoHindernis() is { } hindernis)
        {
            throw new InvalidOperationException(hindernis);
        }

        if (string.IsNullOrWhiteSpace(grund))
        {
            throw new ArgumentException("Ein Storno braucht einen Grund.", nameof(grund));
        }

        StorniertAm = jetzt;
        StorniertVon = benutzerId;
        Stornogrund = grund.Trim();
    }

    /// <summary>Eine bestandene Prüfung (auch mit Mängeln) oder eine Aussonderung hebt Sperren bis zur Prüfung auf (nach dem Einsatz, Eingangsprüfung).</summary>
    public bool HebtSperreAuf => Ergebnis is PruefErgebnis.Bestanden or PruefErgebnis.BestandenMitMaengeln or PruefErgebnis.Ausgesondert;

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

    /// <summary>Sperre nach Benutzung im Einsatz (V13); erledigt sich mit der nächsten bestandenen Prüfung.</summary>
    public Guid? EinsatzId { get; set; }

    /// <summary>Sperre, die erst die nächste bestandene Prüfung aufhebt: nach Benutzung im Einsatz (V13) oder bis zur Eingangsprüfung (V60).</summary>
    public bool SperreBisPruefung { get; set; }

    /// <summary>Prüfung, die diese Sperre aufgehoben hat; ein Storno der Prüfung öffnet sie wieder.</summary>
    public Guid? ErledigtDurchPruefungId { get; set; }

    /// <summary>Text der Sperre bis zur Eingangsprüfung.</summary>
    public const string EingangspruefungText = "Neu geliefert: Eingangsprüfung vor der ersten Nutzung (DGUV Grundsatz 305-002).";

    /// <summary>Sperrt ein neu geliefertes Gerät bis zur ersten bestandenen Prüfung (V60).</summary>
    public static Mangel Eingangssperre(Guid geraetId, DateTimeOffset jetzt, Guid? gemeldetVonId) => new()
    {
        GeraetId = geraetId,
        Beschreibung = EingangspruefungText,
        Schwere = MangelSchwere.AusserBetrieb,
        Status = MangelStatus.Offen,
        GemeldetAm = jetzt,
        GemeldetVonId = gemeldetVonId,
        SperreBisPruefung = true,
    };

    /// <summary>Ein Mangel mit Status Offen oder InArbeit schränkt das Gerät ein.</summary>
    public bool IstOffen => Status != MangelStatus.Behoben;
}
