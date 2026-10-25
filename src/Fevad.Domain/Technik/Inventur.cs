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
/// Bestandsaufnahme einer Inventurgruppe (Fahrzeug, Raum, Regal, Ort) oder des ganzen Bestands. Beim Anlegen entsteht je Gerät
/// eine Position mit dem Soll-Standort; nach dem Abschluss ist die Inventur unveränderlich.
/// </summary>
public class Inventur : Entitaet
{
    public required string Bezeichnung { get; set; }
    public DateOnly Stichtag { get; set; }

    /// <summary>Nur inventurpflichtige Geräte dieser Gruppe; leer = alle inventurpflichtigen Geräte.</summary>
    public Guid? InventurgruppeId { get; set; }
    public Inventurgruppe? Inventurgruppe { get; set; }

    public DateTimeOffset? AbgeschlossenAm { get; set; }

    /// <summary>Wer gezählt hat, für das Inventurprotokoll (V60), z. B. „Müller, Schmid“.</summary>
    public string? Zaehler { get; set; }

    /// <summary>Wer die Zählung geprüft hat (Vier-Augen-Prinzip der Stichtagsinventur).</summary>
    public string? Pruefer { get; set; }
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
