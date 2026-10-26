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

/// <summary>Rechtsgrundlage des Einsatzes nach FwG; entscheidet über den Kostenersatz.</summary>
public enum Rechtsgrundlage
{
    /// <summary>§ 2 Abs. 1 FwG: Pflichtaufgabe, grundsätzlich unentgeltlich.</summary>
    Para2Abs1,

    /// <summary>§ 2 Abs. 2 FwG: weitere Aufgaben, immer kostenersatzpflichtig.</summary>
    Para2Abs2,
}

/// <summary>Bearbeitungsstand des Einsatzberichts.</summary>
public enum EinsatzStatus
{
    /// <summary>Automatisch oder leer angelegt, noch von niemandem gespeichert.</summary>
    [Display(Name = "Entwurf")] Entwurf,
    /// <summary>Wird bearbeitet.</summary>
    [Display(Name = "in Bearbeitung")] InBearbeitung,
    /// <summary>Freigegeben; geht in Entschädigung, Nachweise und Statistik ein.</summary>
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

    /// <summary>Straße des Einsatzorts.</summary>
    public string? Strasse { get; set; }
    /// <summary>Hausnummer des Einsatzorts.</summary>
    public string? Hausnummer { get; set; }
    /// <summary>Postleitzahl des Einsatzorts.</summary>
    public string? Plz { get; set; }
    /// <summary>Ort des Einsatzorts.</summary>
    public string? Ort { get; set; }
    /// <summary>Ortsteil des Einsatzorts.</summary>
    public string? Ortsteil { get; set; }

    /// <summary>FE2: building.</summary>
    public string? Objekt { get; set; }
    /// <summary>Geografische Breite des Einsatzorts.</summary>
    public double? Latitude { get; set; }
    /// <summary>Geografische Länge des Einsatzorts.</summary>
    public double? Longitude { get; set; }

    /// <summary>Zeitpunkt der Alarmierung (UTC).</summary>
    public DateTimeOffset Alarmiert { get; set; }
    /// <summary>Zeitpunkt des Ausrückens (UTC).</summary>
    public DateTimeOffset? Ausgerueckt { get; set; }
    /// <summary>Zeitpunkt des Eintreffens am Einsatzort (UTC).</summary>
    public DateTimeOffset? EintreffenEinsatzort { get; set; }
    /// <summary>Ende des Einsatzes am Einsatzort (UTC).</summary>
    public DateTimeOffset? Einsatzende { get; set; }

    /// <summary>Bis hierhin rechnen Entschädigung und Kostenersatz.</summary>
    public DateTimeOffset? EinsatzbereitschaftHergestellt { get; set; }

    /// <summary>Pflicht bei Freigabe.</summary>
    public Guid? StatistikKategorieId { get; set; }
    /// <summary>Kategorie der Landesstatistik.</summary>
    public StatistikKategorie? StatistikKategorie { get; set; }

    /// <summary>Rechtsgrundlage nach FwG.</summary>
    public Rechtsgrundlage Rechtsgrundlage { get; set; }

    /// <summary>Kennung des Einsatzleiters.</summary>
    public Guid? EinsatzleiterId { get; set; }
    /// <summary>Der Einsatzleiter.</summary>
    public Mitglied? Einsatzleiter { get; set; }

    /// <summary>Lagebericht.</summary>
    public string? Lagebericht { get; set; }
    /// <summary>Getroffene Maßnahmen.</summary>
    public string? Massnahmen { get; set; }

    /// <summary>Anzahl geretteter Personen.</summary>
    public int Gerettete { get; set; }
    /// <summary>Anzahl verletzter Personen (ohne Feuerwehr).</summary>
    public int Verletzte { get; set; }
    /// <summary>Anzahl Toter.</summary>
    public int Tote { get; set; }
    /// <summary>Anzahl verletzter Feuerwehrangehöriger.</summary>
    public int VerletzteFeuerwehr { get; set; }

    /// <summary>Eingesetzte Mittel, z. B. Rohre, Sonderlöschmittel, Bindemittel (V15).</summary>
    public string? EingesetzteMittel { get; set; }
    /// <summary>Schadenart, z. B. Gebäude, Fahrzeug, Wald/Flur, Gewässer (V15).</summary>
    public string? Schadenart { get; set; }
    /// <summary>Anzahl geretteter Tiere.</summary>
    public int TiereGerettet { get; set; }
    /// <summary>Anzahl toter Tiere.</summary>
    public int TiereTot { get; set; }

    /// <summary>Gefahrstoff beteiligt; schaltet den Gefahrstoff-Block frei (V15).</summary>
    public bool Gefahrstoff { get; set; }
    /// <summary>Bezeichnung des Stoffs.</summary>
    public string? GefahrstoffBezeichnung { get; set; }
    /// <summary>UN-Nummer (vierstellig).</summary>
    public string? UnNummer { get; set; }
    /// <summary>Ausgetretene bzw. betroffene Menge mit Einheit.</summary>
    public string? GefahrstoffMenge { get; set; }
    /// <summary>Freisetzung in den Boden.</summary>
    public bool FreisetzungBoden { get; set; }
    /// <summary>Freisetzung in ein Gewässer oder die Kanalisation.</summary>
    public bool FreisetzungWasser { get; set; }
    /// <summary>Freisetzung in die Luft.</summary>
    public bool FreisetzungLuft { get; set; }
    /// <summary>Dekontamination von Einsatzkräften oder Gerät durchgeführt.</summary>
    public bool Dekontamination { get; set; }
    /// <summary>Behördenmeldung, z. B. „Umweltamt LRA, 14:20 Uhr“.</summary>
    public string? Behoerdenmeldung { get; set; }
    /// <summary>Überörtlicher Einsatz außerhalb der Gemeinde.</summary>
    public bool Ueberoertlich { get; set; }

    /// <summary>Objekt mit Brandmeldeanlage, deren Auslösung zum Einsatz führte (V24).</summary>
    public Guid? BmaObjektId { get; set; }
    /// <summary>Das Objekt mit Brandmeldeanlage.</summary>
    public BmaObjekt? BmaObjekt { get; set; }
    /// <summary>Die Brandmeldeanlage hat ohne Brand ausgelöst (Fehlalarm, § 34 Abs. 1 Nr. 6 FwG).</summary>
    public bool BmaFehlalarm { get; set; }

    /// <summary>Ein Kostenersatz ist offen; solange bleiben die Beteiligtendaten stehen (Löschkonzept L3).</summary>
    public bool KostenersatzOffen { get; set; }

    /// <summary>Kostenersatz abgeschlossen; ab hier läuft die Löschfrist der Beteiligten neu (Löschkonzept L3).</summary>
    public DateOnly? KostenersatzAbgeschlossenAm { get; set; }

    /// <summary>Bearbeitungsstand des Berichts.</summary>
    public EinsatzStatus Status { get; set; }
    /// <summary>Zeitpunkt der Freigabe (UTC).</summary>
    public DateTimeOffset? FreigegebenAm { get; set; }
    /// <summary>Konto, das freigegeben hat.</summary>
    public Guid? FreigegebenVon { get; set; }
    /// <summary>Im Notbetrieb erfasst; die Nummer ist vorläufig.</summary>
    public bool ErfasstImNotbetrieb { get; set; }

    /// <summary>Eingesetzte Fahrzeuge.</summary>
    public List<EinsatzFahrzeug> Fahrzeuge { get; set; } = [];
    /// <summary>Teilnehmende Einsatzkräfte.</summary>
    public List<EinsatzTeilnahme> Teilnahmen { get; set; } = [];
    /// <summary>Andere Organisationen am Einsatzort.</summary>
    public List<EinsatzOrganisation> Organisationen { get; set; } = [];
    /// <summary>Beteiligte Personen und Firmen.</summary>
    public List<EinsatzBeteiligter> Beteiligte { get; set; } = [];
    /// <summary>Ausrüstung und Verbrauchsmaterial für den Kostenersatz.</summary>
    public List<EinsatzAusruestung> Ausruestung { get; set; } = [];
    /// <summary>Im Einsatz benutzte Geräte und Mengenartikel (V13).</summary>
    public List<EinsatzGeraet> Geraete { get; set; } = [];

    /// <summary>Führungsstufe nach FwDV 100 (V14).</summary>
    public Fuehrungsstufe? Fuehrungsstufe { get; set; }

    /// <summary>Verlauf der Einsatzleitung mit Übergaben (V14).</summary>
    public List<Einsatzleitung> Einsatzleitungen { get; set; } = [];

    /// <summary>Einsatzabschnitte mit Abschnittsleitern (V14).</summary>
    public List<Einsatzabschnitt> Abschnitte { get; set; } = [];

    /// <summary>Einsatztagebuch (V14).</summary>
    public List<Tagebucheintrag> Tagebuch { get; set; } = [];

    /// <summary>Tagebuch, Einsatzleitung und Abschnitte sind änderbar, solange der Bericht nicht freigegeben ist.</summary>
    public bool TagebuchOffen => Status != EinsatzStatus.Freigegeben;

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

    /// <summary>Gibt den Bericht frei, wenn keine Hindernisse bestehen.</summary>
    /// <param name="benutzerId">Konto, das freigibt.</param>
    /// <param name="zeitpunkt">Zeitpunkt der Freigabe.</param>
    /// <param name="expositionPflicht">Die Einsatzart verlangt die Exposition je Teilnehmer.</param>
    /// <exception cref="InvalidOperationException">Es gibt Freigabehindernisse.</exception>
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

/// <summary>Ein Fahrzeug im Einsatz mit seinen Zeiten.</summary>
public class EinsatzFahrzeug : Entitaet
{
    /// <summary>Kennung des Einsatzes.</summary>
    public Guid EinsatzId { get; set; }
    /// <summary>Der Einsatz.</summary>
    public Einsatz? Einsatz { get; set; }

    /// <summary>Zuordnung zu FE2 vehicles[] über den exakten Funkrufnamen.</summary>
    public Guid FahrzeugId { get; set; }
    /// <summary>Das Fahrzeug.</summary>
    public Fahrzeug? Fahrzeug { get; set; }

    /// <summary>Ausgerückt (UTC).</summary>
    public DateTimeOffset? Ausgerueckt { get; set; }
    /// <summary>Am Einsatzort eingetroffen (UTC).</summary>
    public DateTimeOffset? Eingetroffen { get; set; }
    /// <summary>Zurück am Standort (UTC).</summary>
    public DateTimeOffset? Zurueck { get; set; }
    /// <summary>Wieder einsatzbereit (UTC); Ende der Leistungsdauer im Kostenersatz.</summary>
    public DateTimeOffset? Einsatzbereit { get; set; }
    /// <summary>Gefahrene Kilometer.</summary>
    public int? Kilometer { get; set; }
}

/// <summary>Herkunft einer Teilnahme.</summary>
public enum TeilnahmeQuelle
{
    /// <summary>Von Hand erfasst.</summary>
    Manuell,
    /// <summary>Aus einer FE2-Rückmeldung übernommen.</summary>
    Fe2Rueckmeldung,
}

/// <summary>Teilnahme einer Einsatzkraft am Einsatz.</summary>
public class EinsatzTeilnahme : Entitaet
{
    /// <summary>Kennung des Einsatzes.</summary>
    public Guid EinsatzId { get; set; }
    /// <summary>Der Einsatz.</summary>
    public Einsatz? Einsatz { get; set; }

    /// <summary>Kennung des Mitglieds.</summary>
    public Guid MitgliedId { get; set; }
    /// <summary>Das Mitglied.</summary>
    public Mitglied? Mitglied { get; set; }

    /// <summary>Kennung des Fahrzeugs, auf dem das Mitglied fuhr.</summary>
    public Guid? EinsatzFahrzeugId { get; set; }
    /// <summary>Das Fahrzeug im Einsatz.</summary>
    public EinsatzFahrzeug? EinsatzFahrzeug { get; set; }

    /// <summary>Funktion im Einsatz, z. B. „Maschinist“.</summary>
    public string? Funktion { get; set; }
    /// <summary>Beginn der Teilnahme (UTC); leer erbt die Zeiten des Einsatzes.</summary>
    public DateTimeOffset? Von { get; set; }
    /// <summary>Ende der Teilnahme (UTC).</summary>
    public DateTimeOffset? Bis { get; set; }
    /// <summary>Unter Atemschutz eingesetzt.</summary>
    public bool Atemschutz { get; set; }
    /// <summary>Tragezeit unter Atemschutz in Minuten.</summary>
    public int? AtemschutzMinuten { get; set; }
    /// <summary>Herkunft der Teilnahme.</summary>
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

    /// <summary>Die Einsatzkraft wurde dekontaminiert.</summary>
    public bool Dekontaminiert { get; set; }

    /// <summary>Die Einsatzkleidung geht zur Sammelwäsche (V13); auch ohne Atemschutz-Recht setzbar.</summary>
    public bool PsaWaesche { get; set; }

    /// <summary>Die Kleiderkammer hat die Wäsche erledigt.</summary>
    public DateTimeOffset? PsaWaescheErledigtAm { get; set; }

    /// <summary>Hautkontakt, Exposition oder Vermerk des Einsatzleiters: die Einsatzkleidung muss in die Wäsche.</summary>
    public bool BrauchtWaesche => PsaWaesche || Hautkontakt || Exposition == ExpositionStatus.Exponiert;
}
