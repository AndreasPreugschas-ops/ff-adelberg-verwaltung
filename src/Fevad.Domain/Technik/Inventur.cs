using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Technik;

public enum InventurBefund
{
    [Display(Name = "vorhanden")] Vorhanden,
    [Display(Name = "beschädigt")] Beschaedigt,
    [Display(Name = "fehlt")] Fehlt,
}

/// <summary>
/// Bestandsaufnahme eines Fahrzeugs, eines Lagerorts oder des ganzen Bestands. Beim Anlegen entsteht je Gerät
/// eine Position mit dem Soll-Standort; nach dem Abschluss ist die Inventur unveränderlich.
/// </summary>
public class Inventur : Entitaet
{
    public required string Bezeichnung { get; set; }
    public DateOnly Stichtag { get; set; }

    /// <summary>Nur Geräte dieses Fahrzeugs; leer = alle.</summary>
    public Guid? FahrzeugId { get; set; }
    public Fahrzeug? Fahrzeug { get; set; }

    public DateTimeOffset? AbgeschlossenAm { get; set; }
    public string? Bemerkung { get; set; }

    public List<InventurPosition> Positionen { get; set; } = [];

    public bool Abgeschlossen => AbgeschlossenAm is not null;
}

public class InventurPosition : Entitaet
{
    public Guid InventurId { get; set; }
    public Inventur? Inventur { get; set; }

    public Guid GeraetId { get; set; }
    public Geraet? Geraet { get; set; }

    /// <summary>Soll-Standort beim Anlegen der Inventur (Fahrzeug und Fach als Text).</summary>
    public string? SollStandort { get; set; }

    /// <summary>Leer = noch nicht erfasst.</summary>
    public InventurBefund? Befund { get; set; }
    public DateTimeOffset? ErfasstAm { get; set; }
    public Guid? ErfasstVon { get; set; }
    public string? Bemerkung { get; set; }
}
