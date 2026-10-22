using System.ComponentModel.DataAnnotations;
using Fevad.Domain.Gemeinsam;
using Fevad.Domain.Mitglieder;

namespace Fevad.Domain.Ausbildung;

public enum QualifikationArt
{
    [Display(Name = "Lehrgang")] Lehrgang,
    [Display(Name = "Fahrerlaubnis")] Fahrerlaubnis,
    [Display(Name = "Sonstiges")] Sonstiges,
    [Display(Name = "Abzeichen")] Abzeichen,
}

public enum AusbildungsEbene
{
    [Display(Name = "Standort")] Standort,
    [Display(Name = "Kreisausbildung")] Kreis,
    [Display(Name = "Landesfeuerwehrschule")] Land,
}

public class Qualifikation : Katalogeintrag
{
    /// <summary>Code des Atemschutzlehrgangs; Inhaber brauchen Vorsorge und jährliche Übungen.</summary>
    public const string CodeAtemschutz = "AGT";

    public const string CodeMaschinist = "MA";
    public const string CodeGruppenfuehrer = "GF";

    /// <summary>Leistungsspange DJF; der Ehrungen-Teil liest sie für die Anrechnung der Jugendzeit.</summary>
    public const string CodeLeistungsspange = "LSP";
    public const string CodeJugendflamme1 = "JFL1";
    public const string CodeJugendflamme2 = "JFL2";
    public const string CodeJugendflamme3 = "JFL3";

    public QualifikationArt Art { get; set; }
    public AusbildungsEbene? Ebene { get; set; }

    /// <summary>Leer bedeutet unbefristet.</summary>
    public int? GueltigkeitMonate { get; set; }

    /// <summary>Kürzel für alamos osFunctions (z. B. AGT, MA). Nur gültige Nachweise werden übertragen.</summary>
    public string? Fe2Funktion { get; set; }

    /// <summary>Stufenfolge: Diese Qualifikation setzt die Vorstufe voraus und löst sie ab (nur die höchste Stufe zählt).</summary>
    public Guid? VorstufeId { get; set; }
    public Qualifikation? Vorstufe { get; set; }

    /// <summary>Frühestens so viele Kalenderjahre nach der Vorstufe (Leistungsabzeichen: 1).</summary>
    public int? VorstufeAbstandJahre { get; set; }

    /// <summary>Nötige Qualifikationen als Codes: "TM1; AGT|SPRF" heißt TM1 und dazu AGT oder SPRF.</summary>
    public string? Voraussetzungen { get; set; }

    public int? MindestalterJahre { get; set; }
    public int? HoechstalterJahre { get; set; }

    /// <summary>Alter nach Jahrgang (Kalenderjahr minus Geburtsjahr). Kennzeichnet zugleich die Abzeichen der Jugendfeuerwehr.</summary>
    public bool AlterNachJahrgang { get; set; }

    /// <summary>Mindestdauer in der Jugendfeuerwehr bis zur Abnahme (Leistungsspange: 12).</summary>
    public int? MindestJugendMonate { get; set; }
}

public class QualifikationNachweis : Entitaet
{
    public Guid MitgliedId { get; set; }
    public Mitglied? Mitglied { get; set; }

    public Guid QualifikationId { get; set; }
    public Qualifikation? Qualifikation { get; set; }

    public DateOnly ErworbenAm { get; set; }
    public DateOnly? GueltigBis { get; set; }
    public string? Lehrgangsort { get; set; }
    public decimal? Stunden { get; set; }
    public string? Bemerkung { get; set; }

    /// <summary>Nummer oder Ablageort von Urkunde bzw. Besitzzeugnis.</summary>
    public string? Urkunde { get; set; }
    public Guid? DokumentId { get; set; }

    public Fristampel Status(DateOnly stichtag, int warnungTage) =>
        Frist.Bewerten(GueltigBis, stichtag, warnungTage);

    /// <summary>Ablaufdatum aus dem Katalog vorschlagen; ein manuell gesetztes Datum bleibt erhalten.</summary>
    public void GueltigkeitVorbelegen(Qualifikation qualifikation)
    {
        if (GueltigBis is null && qualifikation.GueltigkeitMonate is { } monate)
        {
            GueltigBis = ErworbenAm.AddMonths(monate);
        }
    }
}
