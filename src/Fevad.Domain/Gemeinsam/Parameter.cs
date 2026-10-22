namespace Fevad.Domain.Gemeinsam;

/// <summary>
/// Zeitlich versionierter Konfigurationswert (Altersgrenzen, Fristen, Sätze).
/// Fristen aus Gesetz und Satzung ändern sich; sie gehören nicht in den Code.
/// </summary>
public class Parameter : Entitaet
{
    public required string Schluessel { get; set; }
    public required string Wert { get; set; }
    public DateOnly GueltigAb { get; set; }
    public DateOnly? GueltigBis { get; set; }

    /// <summary>Herkunft, z. B. "Feuerwehrsatzung Adelberg 2019".</summary>
    public string? Quelle { get; set; }

    public bool IstGueltigAm(DateOnly stichtag) =>
        GueltigAb <= stichtag && (GueltigBis is null || stichtag <= GueltigBis);
}

/// <summary>Bekannte Parameter-Schlüssel.</summary>
public static class ParameterSchluessel
{
    public const string ProbezeitMonate = "Mitglied.Probezeit.Monate";
    public const string AltersgrenzeEinsatzdienst = "Mitglied.Altersgrenze.Einsatzdienst";
    public const string AltersabteilungAbAlter = "Mitglied.Altersabteilung.AbAlter";
    public const string FristWarnungTage = "Frist.Warnung.Tage";
    public const string PruefungWarnungTage = "Technik.Pruefung.WarnungTage";
    public const string AtemschutzUebungIntervallMonate = "Atemschutz.Uebung.IntervallMonate";
    public const string AlarmdatenAufbewahrungMonate = "Einsatz.Alarmdaten.AufbewahrungMonate";
    public const string EinsatznummerFormat = "Einsatz.Nummer.Format";
    public const string DokumentMaxMegabyte = "Dokument.Datei.MaxMegabyte";
    public const string HauptversammlungEinladungsfristTage = "Gremien.Hauptversammlung.EinladungsfristTage";
    public const string AusschussEinladungsfristTage = "Gremien.Ausschuss.EinladungsfristTage";
    public const string AmtszeitWarnungTage = "Gremien.Amtszeit.WarnungTage";
    public const string NachweisWarnungTage = "Ausbildung.Nachweis.WarnungTage";
    public const string AtemschutzWarnungTage = "Atemschutz.WarnungTage";
    public const string ErinnerungIntervallTage = "Frist.Erinnerung.IntervallTage";
    public const string ErinnerungAbStunde = "Frist.Erinnerung.AbStunde";
    public const string StandortfortbildungStunden = "Ausbildung.Standortfortbildung.StundenJahr";
    public const string BelastungsuebungIntervallMonate = "Atemschutz.Belastungsuebung.IntervallMonate";
    public const string EinsatzuebungIntervallMonate = "Atemschutz.Einsatzuebung.IntervallMonate";
    public const string AtemschutzUnterweisungIntervallMonate = "Atemschutz.Unterweisung.IntervallMonate";
    public const string VorsorgeIntervallMonate = "Atemschutz.Vorsorge.IntervallMonate";
    public const string VorsorgeIntervallAbAlterMonate = "Atemschutz.Vorsorge.IntervallAbAlterMonate";
    public const string VorsorgeKuerzeresIntervallAbAlter = "Atemschutz.Vorsorge.KuerzeresIntervallAbAlter";

    public const string DienstSerieMaxMonate = "Dienst.Serie.MaxMonate";
    public const string DienstRueckmeldungSchlussStunden = "Dienst.Rueckmeldung.SchlussStunden";
    public const string KalenderVorschauMonate = "Dienst.Kalender.VorschauMonate";
    public const string KalenderRueckblickMonate = "Dienst.Kalender.RueckblickMonate";

    // Löschfristen für Rückmeldungen; der Löschlauf folgt später.
    public const string RueckmeldungBegruendungLoeschenMonate = "Dienst.Rueckmeldung.BegruendungLoeschenMonate";
    public const string RueckmeldungLoeschenMonate = "Dienst.Rueckmeldung.LoeschenMonate";

    // Entschädigungssätze folgen dem Muster Entschaedigung.{Vorgang}.{Feld}, siehe Entschaedigungssaetze.
}
