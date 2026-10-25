using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Unfaelle;

/// <summary>Bearbeitungsstand eines Sachschadens.</summary>
public enum SachschadenStatus
{
    /// <summary>Vom Mitglied gemeldet, noch nicht weitergeleitet.</summary>
    [Display(Name = "gemeldet")] Gemeldet,
    /// <summary>An die Gemeinde weitergeleitet, Entscheidung offen.</summary>
    [Display(Name = "bei der Gemeinde")] BeiGemeinde,
    /// <summary>Von der Gemeinde anerkannt, Erstattung offen.</summary>
    [Display(Name = "anerkannt")] Anerkannt,
    /// <summary>Von der Gemeinde abgelehnt; abgeschlossen.</summary>
    [Display(Name = "abgelehnt")] Abgelehnt,
    /// <summary>Erstattet; abgeschlossen.</summary>
    [Display(Name = "erstattet")] Erstattet,
}

/// <summary>
/// Schaden an privatem Eigentum eines Mitglieds im Dienst, z. B. Brille oder Kleidung. Die Gemeinde ersetzt ihn nach § 17 FwG;
/// FEVAD hält Meldung, Weiterleitung, Entscheidung und Erstattung fest. Gelöscht wird nach Löschkonzept L40.
/// </summary>
public class Sachschaden : Entitaet
{
    /// <summary>Geschädigtes Mitglied.</summary>
    public Guid MitgliedId { get; set; }
    /// <summary>Das geschädigte Mitglied.</summary>
    public Mitglied? Mitglied { get; set; }

    /// <summary>Gelegenheit des Schadens.</summary>
    public UnfallAnlass Anlass { get; set; }
    /// <summary>Schlüssel des Einsatzes, wenn der Schaden bei einem Einsatz entstand.</summary>
    public Guid? EinsatzId { get; set; }
    /// <summary>Schlüssel des Dienstes, wenn der Schaden bei einem Dienst entstand.</summary>
    public Guid? DienstId { get; set; }

    /// <summary>Zeitpunkt des Schadens in UTC.</summary>
    public DateTimeOffset Zeitpunkt { get; set; }
    /// <summary>Ort des Schadens.</summary>
    public required string Ort { get; set; }

    /// <summary>Beschädigter Gegenstand, z. B. „Brille“.</summary>
    public required string Gegenstand { get; set; }
    /// <summary>Wie der Schaden entstand.</summary>
    public required string Hergang { get; set; }
    /// <summary>Vom Mitglied genannter Schaden in Euro (Reparatur oder Zeitwert).</summary>
    public decimal Betrag { get; set; }

    /// <summary>Bearbeitungsstand.</summary>
    public SachschadenStatus Status { get; set; }
    /// <summary>Tag der Weiterleitung an die Gemeinde.</summary>
    public DateOnly? WeitergeleitetAm { get; set; }
    /// <summary>Tag der Entscheidung der Gemeinde.</summary>
    public DateOnly? EntschiedenAm { get; set; }
    /// <summary>Von der Gemeinde ersetzter Betrag in Euro.</summary>
    public decimal? ErstattetBetrag { get; set; }
    /// <summary>Tag der Erstattung oder Ablehnung; ab dann läuft die Löschfrist.</summary>
    public DateOnly? AbgeschlossenAm { get; set; }

    /// <summary>Die Gemeinde nimmt Rückgriff, z. B. beim Verursacher oder bei grober Fahrlässigkeit (§ 17 FwG).</summary>
    public bool Regress { get; set; }
    /// <summary>Angaben zum Rückgriff.</summary>
    public string? RegressBemerkung { get; set; }
    /// <summary>Freitext, z. B. Begründung der Gemeinde.</summary>
    public string? Bemerkung { get; set; }

    /// <summary>Fotos des Schadens.</summary>
    public List<Meldungsfoto> Fotos { get; set; } = [];

    /// <summary>Der Schaden ist erledigt: erstattet oder abgelehnt.</summary>
    public bool Abgeschlossen => Status is SachschadenStatus.Erstattet or SachschadenStatus.Abgelehnt;

    /// <summary>Status setzen und die zugehörigen Tage eintragen; ein Zurücksetzen nimmt spätere Tage wieder weg.</summary>
    /// <param name="neu">Neuer Status.</param>
    /// <param name="heute">Heutiger Tag.</param>
    public void StatusSetzen(SachschadenStatus neu, DateOnly heute)
    {
        Status = neu;
        WeitergeleitetAm = neu == SachschadenStatus.Gemeldet ? null : WeitergeleitetAm ?? heute;
        EntschiedenAm = neu is SachschadenStatus.Gemeldet or SachschadenStatus.BeiGemeinde ? null : EntschiedenAm ?? heute;
        AbgeschlossenAm = Abgeschlossen ? AbgeschlossenAm ?? heute : null;
    }
}

/// <summary>Foto zu einer Unfall- oder Schadensmeldung; Inhalt nicht im Änderungsprotokoll.</summary>
public class Meldungsfoto : Entitaet
{
    /// <summary>Unfall, zu dem das Foto gehört.</summary>
    public Guid? UnfallId { get; set; }
    /// <summary>Sachschaden, zu dem das Foto gehört.</summary>
    public Guid? SachschadenId { get; set; }

    /// <summary>Bereinigter Dateiname.</summary>
    public required string Dateiname { get; set; }
    /// <summary>MIME-Typ nach Dateiendung (nur PNG und JPEG).</summary>
    public required string Inhaltstyp { get; set; }
    /// <summary>Inhalt.</summary>
    [NichtProtokollieren]
    public required byte[] Inhalt { get; set; }
}
