using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Atemschutz;

public enum VorsorgeErgebnis
{
    [Display(Name = "keine Bedenken")] Geeignet,
    [Display(Name = "befristet keine Bedenken")] GeeignetBefristet,
    [Display(Name = "Bedenken")] NichtGeeignet,
}

/// <summary>
/// Arbeitsmedizinische Vorsorge. Gesundheitsdatum nach Art. 9 DSGVO:
/// gespeichert werden nur Datum, Ergebnis-Status und Frist, niemals der Befund.
/// </summary>
public class AtemschutzVorsorge : Entitaet
{
    public Guid MitgliedId { get; set; }
    public Mitglied? Mitglied { get; set; }

    public DateOnly UntersuchtAm { get; set; }
    public VorsorgeErgebnis Ergebnis { get; set; }
    public DateOnly NaechsteFaellig { get; set; }

    /// <summary>Kurze organisatorische Notiz, kein Befund.</summary>
    public string? Bemerkung { get; set; }

    /// <summary>
    /// Vorschlag für die nächste Vorsorge: Standardintervall, ab einem Lebensalter das kürzere Intervall.
    /// Maßgeblich ist das Alter am Untersuchungstag. Die Bescheinigung des Arztes geht vor.
    /// </summary>
    public static DateOnly NaechsteVorschlagen(DateOnly untersuchtAm, DateOnly geburtsdatum, int intervallMonate, int intervallAbAlterMonate, int abAlter) =>
        untersuchtAm.AddMonths(Mitglied.Alter(geburtsdatum, untersuchtAm) >= abAlter ? intervallAbAlterMonate : intervallMonate);
}

public enum AtemschutzNachweisArt
{
    [Display(Name = "Einsatz")] Einsatz,
    [Display(Name = "Belastungsübung (Übungsanlage)")] Belastungsuebung,
    [Display(Name = "Einsatzübung")] Einsatzuebung,
    [Display(Name = "Unterweisung")] Unterweisung,

    /// <summary>Exposition ohne Atemschutzgerät (z. B. Maschinist in der Rauchschicht); nur in FEVAD, kein KoAtEx-Export, 40 Jahre wie L8.</summary>
    [Display(Name = "Exposition ohne Atemschutzgerät")] Exposition,
}

/// <summary>Eintrag im Atemschutznachweis nach FwDV 7.</summary>
public class AtemschutzNachweis : Entitaet
{
    public Guid MitgliedId { get; set; }
    public Mitglied? Mitglied { get; set; }

    public DateOnly Datum { get; set; }
    public AtemschutzNachweisArt Art { get; set; }
    public string? Geraetetyp { get; set; }
    public int? Minuten { get; set; }
    public string? Taetigkeit { get; set; }
    public string? Ort { get; set; }

    /// <summary>Inventar- oder Gerätenummer des Pressluftatmers, damit sich Gerät und Träger zuordnen lassen.</summary>
    public string? PaGeraet { get; set; }

    /// <summary>Nummer der Atemschutzmaske.</summary>
    public string? Maske { get; set; }

    /// <summary>
    /// Gefahrstoffe, denen der Träger ausgesetzt war (z. B. Brandrauch, Asbest), für den Expositionsnachweis nach § 14 Abs. 3 GefStoffV.
    /// Kein Befund; aufbewahrt wie der Nachweis (L8, 40 Jahre).
    /// </summary>
    public string? Exposition { get; set; }

    /// <summary>Gehört in den Expositionsnachweis: Einsätze und Einsatzübungen sowie alles mit eingetragenem Gefahrstoff.</summary>
    public bool Expositionsrelevant => Art is AtemschutzNachweisArt.Einsatz or AtemschutzNachweisArt.Einsatzuebung or AtemschutzNachweisArt.Exposition
        || Exposition is not null;

    public Guid? EinsatzId { get; set; }
    public Guid? DienstId { get; set; }

    /// <summary>Realeinsatz, der als Einsatzübung nach FwDV 7 zählt (ab der Mindestdauer aus den Parametern).</summary>
    public bool GiltAlsEinsatzuebung { get; set; }

    /// <summary>Bestätigung durch Kommandant oder Stellvertreter; Benutzer-Id, leer bei Altbestand.</summary>
    public DateTimeOffset? BestaetigtKommandoAm { get; set; }
    public Guid? BestaetigtKommandoVon { get; set; }

    /// <summary>Bestätigung durch den Atemschutzgerätewart; Benutzer-Id, leer bei Altbestand.</summary>
    public DateTimeOffset? BestaetigtWartAm { get; set; }
    public Guid? BestaetigtWartVon { get; set; }

    /// <summary>Kommandant und Atemschutzwart haben bestätigt (Andi, 25.10.2026: beide).</summary>
    public bool Bestaetigt => BestaetigtKommandoAm is not null && BestaetigtWartAm is not null;

    /// <summary>Erfüllt der Eintrag die jährliche Pflicht <paramref name="pflicht"/>? Ein Realeinsatz ab Mindestdauer zählt als Einsatzübung.</summary>
    public bool Erfuellt(AtemschutzNachweisArt pflicht) =>
        Art == pflicht || (pflicht == AtemschutzNachweisArt.Einsatzuebung && GiltAlsEinsatzuebung);

    /// <summary>Bestätigungen zurücksetzen, weil sich der Inhalt geändert hat.</summary>
    public void BestaetigungenZuruecksetzen()
    {
        BestaetigtKommandoAm = null;
        BestaetigtKommandoVon = null;
        BestaetigtWartAm = null;
        BestaetigtWartVon = null;
    }

    /// <summary>
    /// Bestätigt mit den Rechten des Benutzers. Dieselbe Person füllt nie beide Felder, auch wenn sie beide Rechte hat.
    /// Gibt zurück, ob sich etwas geändert hat.
    /// </summary>
    public bool Bestaetigen(Guid benutzerId, bool alsKommando, bool alsWart, DateTimeOffset jetzt)
    {
        if (alsKommando && BestaetigtKommandoAm is null && BestaetigtWartVon != benutzerId)
        {
            (BestaetigtKommandoAm, BestaetigtKommandoVon) = (jetzt, benutzerId);
            return true;
        }

        if (alsWart && BestaetigtWartAm is null && BestaetigtKommandoVon != benutzerId)
        {
            (BestaetigtWartAm, BestaetigtWartVon) = (jetzt, benutzerId);
            return true;
        }

        return false;
    }
}

/// <summary>
/// „Vorübergehend nicht einsetzbar“ ohne Grund (Gesundheitsdaten werden nicht gespeichert). Solange wirksam, ist der Geräteträger rot.
/// Selbstmeldungen sind sofort wirksam; was länger dauert als der Parameter erlaubt, braucht die Genehmigung von Kommando oder
/// Atemschutzwart (Andi, 25.10.2026).
/// </summary>
public class AtemschutzSperre : Entitaet
{
    public Guid MitgliedId { get; set; }
    public Mitglied? Mitglied { get; set; }

    public DateOnly Von { get; set; }
    public DateOnly Bis { get; set; }

    /// <summary>Vom Geräteträger selbst gemeldet; sonst von Kommando oder Atemschutzwart eingetragen.</summary>
    public bool Selbstmeldung { get; set; }
    public Guid? GemeldetVon { get; set; }
    public DateTimeOffset GemeldetAm { get; set; }

    public DateTimeOffset? GenehmigtAm { get; set; }
    public Guid? GenehmigtVon { get; set; }

    public DateTimeOffset? AufgehobenAm { get; set; }
    public Guid? AufgehobenVon { get; set; }

    public int Tage => Bis.DayNumber - Von.DayNumber + 1;

    public bool Wirksam(DateOnly tag) => AufgehobenAm is null && Von <= tag && tag <= Bis;

    /// <summary>Selbstmeldung über die Höchstdauer hinaus, noch nicht genehmigt.</summary>
    public bool GenehmigungOffen(int hoechstTage) => Selbstmeldung && AufgehobenAm is null && GenehmigtAm is null && Tage > hoechstTage;
}
