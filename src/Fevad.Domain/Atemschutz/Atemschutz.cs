using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Atemschutz;

/// <summary>Ergebnis der arbeitsmedizinischen Vorsorge, nur als Status ohne Befund.</summary>
public enum VorsorgeErgebnis
{
    /// <summary>Keine gesundheitlichen Bedenken.</summary>
    [Display(Name = "keine Bedenken")] Geeignet,
    /// <summary>Keine Bedenken, aber befristet; die nächste Vorsorge kommt früher.</summary>
    [Display(Name = "befristet keine Bedenken")] GeeignetBefristet,
    /// <summary>Gesundheitliche Bedenken; der Träger ist nicht einsatzbereit.</summary>
    [Display(Name = "Bedenken")] NichtGeeignet,
}

/// <summary>
/// Arbeitsmedizinische Vorsorge. Gesundheitsdatum nach Art. 9 DSGVO:
/// gespeichert werden nur Datum, Ergebnis-Status und Frist, niemals der Befund.
/// </summary>
public class AtemschutzVorsorge : Entitaet
{
    /// <summary>Kennung des untersuchten Mitglieds.</summary>
    public Guid MitgliedId { get; set; }
    /// <summary>Das untersuchte Mitglied.</summary>
    public Mitglied? Mitglied { get; set; }

    /// <summary>Tag der Untersuchung.</summary>
    public DateOnly UntersuchtAm { get; set; }
    /// <summary>Ergebnis als Status.</summary>
    public VorsorgeErgebnis Ergebnis { get; set; }
    /// <summary>Tag, bis zu dem die nächste Vorsorge fällig ist.</summary>
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

/// <summary>Art eines Eintrags im Atemschutznachweis.</summary>
public enum AtemschutzNachweisArt
{
    /// <summary>Atemschutzeinsatz.</summary>
    [Display(Name = "Einsatz")] Einsatz,
    /// <summary>Belastungsübung in der Übungsanlage (jährliche Pflicht).</summary>
    [Display(Name = "Belastungsübung (Übungsanlage)")] Belastungsuebung,
    /// <summary>Einsatzübung unter Atemschutz (jährliche Pflicht).</summary>
    [Display(Name = "Einsatzübung")] Einsatzuebung,
    /// <summary>Unterweisung (jährliche Pflicht).</summary>
    [Display(Name = "Unterweisung")] Unterweisung,

    /// <summary>Exposition ohne Atemschutzgerät (z. B. Maschinist in der Rauchschicht); nur in FEVAD, kein KoAtEx-Export, 40 Jahre wie L8.</summary>
    [Display(Name = "Exposition ohne Atemschutzgerät")] Exposition,
}

/// <summary>Eintrag im Atemschutznachweis nach FwDV 7.</summary>
public class AtemschutzNachweis : Entitaet
{
    /// <summary>Kennung des Geräteträgers.</summary>
    public Guid MitgliedId { get; set; }
    /// <summary>Der Geräteträger.</summary>
    public Mitglied? Mitglied { get; set; }

    /// <summary>Tag des Einsatzes, der Übung oder der Unterweisung.</summary>
    public DateOnly Datum { get; set; }
    /// <summary>Art des Eintrags.</summary>
    public AtemschutzNachweisArt Art { get; set; }
    /// <summary>Typ des getragenen Geräts.</summary>
    public string? Geraetetyp { get; set; }
    /// <summary>Tragezeit unter Atemschutz in Minuten.</summary>
    public int? Minuten { get; set; }
    /// <summary>Tätigkeit unter Atemschutz.</summary>
    public string? Taetigkeit { get; set; }
    /// <summary>Ort des Einsatzes oder der Übung.</summary>
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

    /// <summary>Einsatz, aus dem der Eintrag stammt.</summary>
    public Guid? EinsatzId { get; set; }
    /// <summary>Dienst, aus dem der Eintrag stammt.</summary>
    public Guid? DienstId { get; set; }

    /// <summary>Realeinsatz, der als Einsatzübung nach FwDV 7 zählt (ab der Mindestdauer aus den Parametern).</summary>
    public bool GiltAlsEinsatzuebung { get; set; }

    /// <summary>Bestätigung durch Kommandant oder Stellvertreter; Benutzer-Id, leer bei Altbestand.</summary>
    public DateTimeOffset? BestaetigtKommandoAm { get; set; }
    /// <summary>Benutzerkonto von Kommandant oder Stellvertreter, das bestätigt hat.</summary>
    public Guid? BestaetigtKommandoVon { get; set; }

    /// <summary>Bestätigung durch den Atemschutzgerätewart; Benutzer-Id, leer bei Altbestand.</summary>
    public DateTimeOffset? BestaetigtWartAm { get; set; }
    /// <summary>Benutzerkonto des Atemschutzgerätewarts, das bestätigt hat.</summary>
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
    /// <summary>Geräteträger, der nicht einsetzbar ist.</summary>
    public Guid MitgliedId { get; set; }
    /// <summary>Navigation zum Geräteträger.</summary>
    public Mitglied? Mitglied { get; set; }

    /// <summary>Erster Tag, an dem der Geräteträger nicht einsetzbar ist.</summary>
    public DateOnly Von { get; set; }
    /// <summary>Letzter Tag, an dem der Geräteträger nicht einsetzbar ist (einschließlich).</summary>
    public DateOnly Bis { get; set; }

    /// <summary>Vom Geräteträger selbst gemeldet; sonst von Kommando oder Atemschutzwart eingetragen.</summary>
    public bool Selbstmeldung { get; set; }
    /// <summary>Benutzerkonto, das die Abmeldung erfasst hat.</summary>
    public Guid? GemeldetVon { get; set; }
    /// <summary>Zeitpunkt der Meldung (UTC).</summary>
    public DateTimeOffset GemeldetAm { get; set; }

    /// <summary>Genehmigung einer Selbstmeldung über die Höchstdauer hinaus (UTC).</summary>
    public DateTimeOffset? GenehmigtAm { get; set; }
    /// <summary>Benutzerkonto, das genehmigt hat.</summary>
    public Guid? GenehmigtVon { get; set; }

    /// <summary>Vorzeitig aufgehoben (UTC); danach nicht mehr wirksam.</summary>
    public DateTimeOffset? AufgehobenAm { get; set; }
    /// <summary>Benutzerkonto, das aufgehoben hat.</summary>
    public Guid? AufgehobenVon { get; set; }

    /// <summary>Dauer in Kalendertagen, beide Grenzen eingeschlossen.</summary>
    public int Tage => Bis.DayNumber - Von.DayNumber + 1;

    /// <summary>Sperrt den Geräteträger am angegebenen Tag.</summary>
    public bool Wirksam(DateOnly tag) => AufgehobenAm is null && Von <= tag && tag <= Bis;

    /// <summary>Selbstmeldung über die Höchstdauer hinaus, noch nicht genehmigt.</summary>
    public bool GenehmigungOffen(int hoechstTage) => Selbstmeldung && AufgehobenAm is null && GenehmigtAm is null && Tage > hoechstTage;
}
