using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;
using Fevad.Domain.Technik;

namespace Fevad.Domain.Einsaetze;

/// <summary>Kategorie der Landesstatistik BW (Brand, Technische Hilfe, ...).</summary>
public class StatistikKategorie : Katalogeintrag
{
}

public enum Rechtsgrundlage
{
    /// <summary>§ 2 Abs. 1 FwG: Pflichtaufgabe, grundsätzlich unentgeltlich.</summary>
    Para2Abs1,

    /// <summary>§ 2 Abs. 2 FwG: weitere Aufgaben, immer kostenersatzpflichtig.</summary>
    Para2Abs2,
}

public enum EinsatzStatus
{
    [Display(Name = "Entwurf")] Entwurf,
    [Display(Name = "in Bearbeitung")] InBearbeitung,
    [Display(Name = "freigegeben")] Freigegeben,
}

/// <summary>
/// Einsatzbericht. Drehscheibe für Entschädigung, Atemschutznachweise, Prüfungen nach Gebrauch,
/// Kostenersatz und Jahresstatistik. Felder mit Kommentar "FE2" werden aus alamos übernommen.
/// </summary>
public class Einsatz : Entitaet
{
    /// <summary>Format JJJJ-NNN; im Notbetrieb vorläufig N-...</summary>
    public required string Einsatznummer { get; set; }

    /// <summary>FE2: externalId bzw. Einsatznummer der ILS.</summary>
    public string? LeitstellenNummer { get; set; }

    /// <summary>FE2: keyword, z. B. "B 2".</summary>
    public string? Stichwort { get; set; }

    /// <summary>FE2: keyword_description.</summary>
    public string? StichwortBeschreibung { get; set; }

    /// <summary>FE2: keyword_category.</summary>
    public string? StichwortKategorie { get; set; }

    /// <summary>FE2: message[] zusammengefügt.</summary>
    public string? Meldung { get; set; }

    public string? Strasse { get; set; }
    public string? Hausnummer { get; set; }
    public string? Plz { get; set; }
    public string? Ort { get; set; }
    public string? Ortsteil { get; set; }

    /// <summary>FE2: building.</summary>
    public string? Objekt { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public DateTimeOffset Alarmiert { get; set; }
    public DateTimeOffset? Ausgerueckt { get; set; }
    public DateTimeOffset? EintreffenEinsatzort { get; set; }
    public DateTimeOffset? Einsatzende { get; set; }

    /// <summary>Bis hierhin rechnen Entschädigung und Kostenersatz.</summary>
    public DateTimeOffset? EinsatzbereitschaftHergestellt { get; set; }

    /// <summary>Pflicht bei Freigabe.</summary>
    public Guid? StatistikKategorieId { get; set; }
    public StatistikKategorie? StatistikKategorie { get; set; }

    public Rechtsgrundlage Rechtsgrundlage { get; set; }

    public Guid? EinsatzleiterId { get; set; }
    public Mitglied? Einsatzleiter { get; set; }

    public string? Lagebericht { get; set; }
    public string? Massnahmen { get; set; }

    public int Gerettete { get; set; }
    public int Verletzte { get; set; }
    public int Tote { get; set; }
    public int VerletzteFeuerwehr { get; set; }
    public bool Ueberoertlich { get; set; }

    public EinsatzStatus Status { get; set; }
    public DateTimeOffset? FreigegebenAm { get; set; }
    public Guid? FreigegebenVon { get; set; }
    public bool ErfasstImNotbetrieb { get; set; }

    public List<EinsatzFahrzeug> Fahrzeuge { get; set; } = [];
    public List<EinsatzTeilnahme> Teilnahmen { get; set; } = [];

    /// <summary>Gründe, warum der Bericht noch nicht freigegeben werden kann.</summary>
    public IReadOnlyList<string> FreigabeHindernisse()
    {
        var hindernisse = new List<string>();
        if (StatistikKategorieId is null)
        {
            hindernisse.Add("Statistik-Kategorie fehlt.");
        }

        if (Einsatzende is null)
        {
            hindernisse.Add("Einsatzende fehlt.");
        }
        else if (Einsatzende < Alarmiert)
        {
            hindernisse.Add("Einsatzende liegt vor der Alarmierung.");
        }

        if (Teilnahmen.Count == 0)
        {
            hindernisse.Add("Keine Teilnehmer erfasst.");
        }

        return hindernisse;
    }

    public void Freigeben(Guid benutzerId, DateTimeOffset zeitpunkt)
    {
        var hindernisse = FreigabeHindernisse();
        if (hindernisse.Count > 0)
        {
            throw new InvalidOperationException(string.Join(" ", hindernisse));
        }

        Status = EinsatzStatus.Freigegeben;
        FreigegebenAm = zeitpunkt;
        FreigegebenVon = benutzerId;
    }

    /// <summary>Ein Mensch hat den automatisch oder leer angelegten Entwurf zum ersten Mal gespeichert.</summary>
    public void InBearbeitungNehmen()
    {
        if (Status == EinsatzStatus.Entwurf)
        {
            Status = EinsatzStatus.InBearbeitung;
        }
    }

    /// <summary>Öffnet einen freigegebenen Bericht wieder zur Bearbeitung; die alte Freigabe bleibt im Protokoll.</summary>
    public void FreigabeZuruecknehmen()
    {
        if (Status != EinsatzStatus.Freigegeben)
        {
            throw new InvalidOperationException("Der Bericht ist nicht freigegeben.");
        }

        Status = EinsatzStatus.InBearbeitung;
        FreigegebenAm = null;
        FreigegebenVon = null;
    }
}

public class EinsatzFahrzeug : Entitaet
{
    public Guid EinsatzId { get; set; }
    public Einsatz? Einsatz { get; set; }

    /// <summary>Zuordnung zu FE2 vehicles[] über den exakten Funkrufnamen.</summary>
    public Guid FahrzeugId { get; set; }
    public Fahrzeug? Fahrzeug { get; set; }

    public DateTimeOffset? Ausgerueckt { get; set; }
    public DateTimeOffset? Eingetroffen { get; set; }
    public DateTimeOffset? Zurueck { get; set; }
    public DateTimeOffset? Einsatzbereit { get; set; }
    public int? Kilometer { get; set; }
}

public enum TeilnahmeQuelle
{
    Manuell,
    Fe2Rueckmeldung,
}

public class EinsatzTeilnahme : Entitaet
{
    public Guid EinsatzId { get; set; }
    public Einsatz? Einsatz { get; set; }

    public Guid MitgliedId { get; set; }
    public Mitglied? Mitglied { get; set; }

    public Guid? EinsatzFahrzeugId { get; set; }
    public EinsatzFahrzeug? EinsatzFahrzeug { get; set; }

    public string? Funktion { get; set; }
    public DateTimeOffset? Von { get; set; }
    public DateTimeOffset? Bis { get; set; }
    public bool Atemschutz { get; set; }
    public int? AtemschutzMinuten { get; set; }
    public TeilnahmeQuelle Quelle { get; set; }

    /// <summary>Vom Mitglied selbst bestätigt (Self-Service).</summary>
    public bool Bestaetigt { get; set; }
}
