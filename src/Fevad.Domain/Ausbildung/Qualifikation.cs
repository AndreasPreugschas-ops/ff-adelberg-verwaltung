using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Ausbildung;

/// <summary>Art eines Eintrags im Qualifikationskatalog.</summary>
public enum QualifikationArt
{
    /// <summary>Lehrgang, z. B. Truppmann oder Gruppenführer.</summary>
    [Display(Name = "Lehrgang")] Lehrgang,
    /// <summary>Fahrerlaubnisklasse, z. B. C oder Feuerwehrführerschein.</summary>
    [Display(Name = "Fahrerlaubnis")] Fahrerlaubnis,
    /// <summary>Sonstige Qualifikation, z. B. Sprechfunker-Zeugnis.</summary>
    [Display(Name = "Sonstiges")] Sonstiges,
    /// <summary>Abzeichen, z. B. Leistungsabzeichen oder Jugendflamme; am Abnahmetag erworben.</summary>
    [Display(Name = "Abzeichen")] Abzeichen,
}

/// <summary>Wo ein Lehrgang stattfindet.</summary>
public enum AusbildungsEbene
{
    /// <summary>Ausbildung am Standort.</summary>
    [Display(Name = "Standort")] Standort,
    /// <summary>Kreisausbildung.</summary>
    [Display(Name = "Kreisausbildung")] Kreis,
    /// <summary>Landesfeuerwehrschule.</summary>
    [Display(Name = "Landesfeuerwehrschule")] Land,
}

/// <summary>
/// Katalogeintrag für Lehrgänge, Fahrerlaubnisse, Abzeichen und sonstige Qualifikationen, mit Gültigkeit, Stufenfolge und
/// Voraussetzungen.
/// </summary>
public class Qualifikation : Katalogeintrag
{
    /// <summary>Code des Atemschutzlehrgangs; Inhaber brauchen Vorsorge und jährliche Übungen.</summary>
    public const string CodeAtemschutz = "AGT";

    /// <summary>Code des Maschinistenlehrgangs.</summary>
    public const string CodeMaschinist = "MA";
    /// <summary>Code des Gruppenführerlehrgangs.</summary>
    public const string CodeGruppenfuehrer = "GF";

    /// <summary>Leistungsspange DJF; der Ehrungen-Teil liest sie für die Anrechnung der Jugendzeit.</summary>
    public const string CodeLeistungsspange = "LSP";
    /// <summary>Code der Jugendflamme Stufe 1.</summary>
    public const string CodeJugendflamme1 = "JFL1";
    /// <summary>Code der Jugendflamme Stufe 2.</summary>
    public const string CodeJugendflamme2 = "JFL2";
    /// <summary>Code der Jugendflamme Stufe 3.</summary>
    public const string CodeJugendflamme3 = "JFL3";

    /// <summary>Art der Qualifikation.</summary>
    public QualifikationArt Art { get; set; }
    /// <summary>Ebene der Ausbildung bei Lehrgängen.</summary>
    public AusbildungsEbene? Ebene { get; set; }

    /// <summary>Leer bedeutet unbefristet.</summary>
    public int? GueltigkeitMonate { get; set; }

    /// <summary>Kürzel für alamos osFunctions (z. B. AGT, MA). Nur gültige Nachweise werden übertragen.</summary>
    public string? Fe2Funktion { get; set; }

    /// <summary>Stufenfolge: Diese Qualifikation setzt die Vorstufe voraus und löst sie ab (nur die höchste Stufe zählt).</summary>
    public Guid? VorstufeId { get; set; }
    /// <summary>Die Vorstufe.</summary>
    public Qualifikation? Vorstufe { get; set; }

    /// <summary>Frühestens so viele Kalenderjahre nach der Vorstufe (Leistungsabzeichen: 1).</summary>
    public int? VorstufeAbstandJahre { get; set; }

    /// <summary>Nötige Qualifikationen als Codes: "TM1; AGT|SPRF" heißt TM1 und dazu AGT oder SPRF.</summary>
    public string? Voraussetzungen { get; set; }

    /// <summary>Mindestalter am Abnahmetag.</summary>
    public int? MindestalterJahre { get; set; }
    /// <summary>Höchstalter am Abnahmetag.</summary>
    public int? HoechstalterJahre { get; set; }

    /// <summary>Alter nach Jahrgang (Kalenderjahr minus Geburtsjahr). Kennzeichnet zugleich die Abzeichen der Jugendfeuerwehr.</summary>
    public bool AlterNachJahrgang { get; set; }

    /// <summary>Mindestdauer in der Jugendfeuerwehr bis zur Abnahme (Leistungsspange: 12).</summary>
    public int? MindestJugendMonate { get; set; }
}

/// <summary>Erworbene Qualifikation eines Mitglieds mit Erwerbsdatum und ggf. Ablauf.</summary>
public class QualifikationNachweis : Entitaet
{
    /// <summary>Schlüssel des Mitglieds.</summary>
    public Guid MitgliedId { get; set; }
    /// <summary>Das Mitglied.</summary>
    public Mitglied? Mitglied { get; set; }

    /// <summary>Schlüssel der Qualifikation.</summary>
    public Guid QualifikationId { get; set; }
    /// <summary>Die Qualifikation.</summary>
    public Qualifikation? Qualifikation { get; set; }

    /// <summary>Tag des Erwerbs, bei Lehrgängen der letzte Lehrgangstag.</summary>
    public DateOnly ErworbenAm { get; set; }
    /// <summary>Ablauf; leer bei unbefristeten Qualifikationen.</summary>
    public DateOnly? GueltigBis { get; set; }
    /// <summary>Ort des Lehrgangs oder der Abnahme.</summary>
    public string? Lehrgangsort { get; set; }
    /// <summary>Umfang in Stunden, z. B. für die Ausbildungsstatistik.</summary>
    public decimal? Stunden { get; set; }
    /// <summary>Freitext.</summary>
    public string? Bemerkung { get; set; }

    /// <summary>Nummer oder Ablageort von Urkunde bzw. Besitzzeugnis.</summary>
    public string? Urkunde { get; set; }
    /// <summary>Hochgeladene Urkunde in der Dokumentenablage.</summary>
    public Guid? DokumentId { get; set; }

    /// <summary>Regelwerk, nach dem ein Jugendabzeichen abgenommen wurde (V50a); leer bei anderen Qualifikationen.</summary>
    public Guid? AbzeichenregelwerkId { get; set; }
    /// <summary>Das Regelwerk der Abnahme.</summary>
    public Abzeichenregelwerk? Abzeichenregelwerk { get; set; }

    /// <summary>Bewertet die Gültigkeit zum Stichtag.</summary>
    /// <param name="stichtag">Tag der Bewertung.</param>
    /// <param name="warnungTage">Warnfrist in Tagen.</param>
    /// <returns>Die Ampel; unbefristete Nachweise sind grün.</returns>
    public Fristampel Status(DateOnly stichtag, int warnungTage) =>
        Frist.Bewerten(GueltigBis, stichtag, warnungTage);

    /// <summary>Ablaufdatum aus dem Katalog vorschlagen; ein manuell gesetztes Datum bleibt erhalten.</summary>
    /// <param name="qualifikation">Katalogeintrag mit der Gültigkeit in Monaten.</param>
    public void GueltigkeitVorbelegen(Qualifikation qualifikation)
    {
        if (GueltigBis is null && qualifikation.GueltigkeitMonate is { } monate)
        {
            GueltigBis = ErworbenAm.AddMonths(monate);
        }
    }
}
