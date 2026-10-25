using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;
using Fevad.Domain.Technik;

namespace Fevad.Domain.Einsaetze;

/// <summary>Kategorie der Landesstatistik BW (Brand, Technische Hilfe, ...).</summary>
public class StatistikKategorie : Katalogeintrag
{
    /// <summary>Bei dieser Einsatzart muss je Teilnehmer die Exposition erfasst sein (Brand, Gefahrstoff; DGUV Information 205-035).</summary>
    public bool ExpositionPflicht { get; set; }
}

/// <summary>Gefahrstoffgruppe für den Expositionsblock im Einsatzbericht (Brandrauch, Asbest, ...).</summary>
public class Gefahrstoffgruppe : Katalogeintrag
{
}

/// <summary>Exposition einer Einsatzkraft gegenüber Gefahrstoffen (KoAtEx, DGUV Information 205-035).</summary>
public enum ExpositionStatus
{
    /// <summary>Kein Kontakt mit Gefahrstoffen.</summary>
    [Display(Name = "nicht exponiert")] NichtExponiert,
    /// <summary>Kontakt mit Gefahrstoffen; Gefahrstoffgruppe ist Pflicht.</summary>
    [Display(Name = "exponiert")] Exponiert,
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

    /// <summary>Ein Kostenersatz ist offen; solange bleiben die Beteiligtendaten stehen (Löschkonzept L3).</summary>
    public bool KostenersatzOffen { get; set; }

    /// <summary>Kostenersatz abgeschlossen; ab hier läuft die Löschfrist der Beteiligten neu (Löschkonzept L3).</summary>
    public DateOnly? KostenersatzAbgeschlossenAm { get; set; }

    public EinsatzStatus Status { get; set; }
    public DateTimeOffset? FreigegebenAm { get; set; }
    public Guid? FreigegebenVon { get; set; }
    public bool ErfasstImNotbetrieb { get; set; }

    public List<EinsatzFahrzeug> Fahrzeuge { get; set; } = [];
    public List<EinsatzTeilnahme> Teilnahmen { get; set; } = [];
    public List<EinsatzOrganisation> Organisationen { get; set; } = [];
    public List<EinsatzBeteiligter> Beteiligte { get; set; } = [];
    public List<EinsatzAusruestung> Ausruestung { get; set; } = [];
    /// <summary>Im Einsatz benutzte Geräte und Mengenartikel (V13).</summary>
    public List<EinsatzGeraet> Geraete { get; set; } = [];

    /// <summary>Gründe, warum der Bericht noch nicht freigegeben werden kann.</summary>
    /// <param name="expositionPflicht">Die Einsatzart verlangt die Exposition je Teilnehmer (<see cref="StatistikKategorie.ExpositionPflicht"/>).</param>
    public IReadOnlyList<string> FreigabeHindernisse(bool expositionPflicht = false)
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

        if (expositionPflicht && Teilnahmen.Count(t => t.Exposition is null) is > 0 and var ohne)
        {
            hindernisse.Add(ohne == 1 ? "Bei einem Teilnehmer fehlt die Exposition." : $"Bei {ohne} Teilnehmern fehlt die Exposition.");
        }

        if (Teilnahmen.Count(t => t.Exposition == ExpositionStatus.Exponiert && string.IsNullOrWhiteSpace(t.Gefahrstoffe)) is > 0 and var ohneStoff)
        {
            hindernisse.Add(ohneStoff == 1 ? "Bei einem exponierten Teilnehmer fehlt der Gefahrstoff." : $"Bei {ohneStoff} exponierten Teilnehmern fehlt der Gefahrstoff.");
        }

        return hindernisse;
    }

    public void Freigeben(Guid benutzerId, DateTimeOffset zeitpunkt, bool expositionPflicht = false)
    {
        var hindernisse = FreigabeHindernisse(expositionPflicht);
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

    /// <summary>Nummer des Pressluftatmers (Inventarnummer), geht in den Atemschutznachweis.</summary>
    public string? PaGeraet { get; set; }

    /// <summary>Nummer der Atemschutzmaske.</summary>
    public string? Maske { get; set; }

    /// <summary>Exposition, auch ohne Atemschutzgerät; leer = nicht erfasst (KoAtEx, DGUV Information 205-035).</summary>
    public ExpositionStatus? Exposition { get; set; }

    /// <summary>Gefahrstoffgruppen aus dem Katalog, durch Komma getrennt.</summary>
    public string? Gefahrstoffe { get; set; }

    /// <summary>Hautkontakt oder beaufschlagte (kontaminierte) Schutzkleidung.</summary>
    public bool Hautkontakt { get; set; }

    public bool Dekontaminiert { get; set; }

    /// <summary>Die Einsatzkleidung geht zur Sammelwäsche (V13); auch ohne Atemschutz-Recht setzbar.</summary>
    public bool PsaWaesche { get; set; }

    /// <summary>Die Kleiderkammer hat die Wäsche erledigt.</summary>
    public DateTimeOffset? PsaWaescheErledigtAm { get; set; }

    /// <summary>Hautkontakt, Exposition oder Vermerk des Einsatzleiters: die Einsatzkleidung muss in die Wäsche.</summary>
    public bool BrauchtWaesche => PsaWaesche || Hautkontakt || Exposition == ExpositionStatus.Exponiert;
}
