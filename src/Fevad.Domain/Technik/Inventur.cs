using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;

namespace Fevad.Domain.Technik;

/// <summary>Ergebnis der Zählung eines Geräts.</summary>
public enum InventurBefund
{
    /// <summary>Vorhanden und in Ordnung.</summary>
    [Display(Name = "vorhanden")] Vorhanden,
    /// <summary>Vorhanden, aber beschädigt.</summary>
    [Display(Name = "beschädigt")] Beschaedigt,
    /// <summary>Nicht auffindbar.</summary>
    [Display(Name = "fehlt")] Fehlt,
}

/// <summary>
/// Bestandsaufnahme einer Inventurgruppe (Fahrzeug, Raum, Regal, Ort) oder des ganzen Bestands. Beim Anlegen entsteht je Gerät
/// eine Position mit dem Soll-Standort; nach dem Abschluss ist die Inventur unveränderlich.
/// </summary>
public class Inventur : Entitaet
{
    /// <summary>Bezeichnung der Inventur.</summary>
    public required string Bezeichnung { get; set; }
    /// <summary>Stichtag der Bestandsaufnahme.</summary>
    public DateOnly Stichtag { get; set; }

    /// <summary>Nur inventurpflichtige Geräte dieser Gruppe; leer = alle inventurpflichtigen Geräte.</summary>
    public Guid? InventurgruppeId { get; set; }
    /// <summary>Die Inventurgruppe; leer bei der Inventur des ganzen Bestands.</summary>
    public Inventurgruppe? Inventurgruppe { get; set; }

    /// <summary>Zeitpunkt des Abschlusses (UTC).</summary>
    public DateTimeOffset? AbgeschlossenAm { get; set; }

    /// <summary>Wer gezählt hat, für das Inventurprotokoll (V60), z. B. „Müller, Schmid“.</summary>
    public string? Zaehler { get; set; }

    /// <summary>Wer die Zählung geprüft hat (Vier-Augen-Prinzip der Stichtagsinventur).</summary>
    public string? Pruefer { get; set; }
    /// <summary>Bemerkung.</summary>
    public string? Bemerkung { get; set; }

    /// <summary>Je Gerät eine Position.</summary>
    public List<InventurPosition> Positionen { get; set; } = [];

    /// <summary>Die Inventur ist abgeschlossen und damit unveränderlich.</summary>
    public bool Abgeschlossen => AbgeschlossenAm is not null;
}

/// <summary>Ein Gerät in einer Inventur mit Soll-Standort und Befund.</summary>
public class InventurPosition : Entitaet
{
    /// <summary>Kennung der Inventur.</summary>
    public Guid InventurId { get; set; }
    /// <summary>Die Inventur.</summary>
    public Inventur? Inventur { get; set; }

    /// <summary>Kennung des Geräts.</summary>
    public Guid GeraetId { get; set; }
    /// <summary>Das Gerät.</summary>
    public Geraet? Geraet { get; set; }

    /// <summary>Soll-Standort beim Anlegen der Inventur (Fahrzeug und Fach als Text).</summary>
    public string? SollStandort { get; set; }

    /// <summary>Leer = noch nicht erfasst.</summary>
    public InventurBefund? Befund { get; set; }
    /// <summary>Zeitpunkt der Erfassung (UTC).</summary>
    public DateTimeOffset? ErfasstAm { get; set; }
    /// <summary>Benutzer, der erfasst hat.</summary>
    public Guid? ErfasstVon { get; set; }
    /// <summary>Bemerkung.</summary>
    public string? Bemerkung { get; set; }
}
