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
    public const string InventarisierungAbWertEuro = "Technik.Material.InventarisierungAbWertEuro";
    public const string DirektabschreibungBisWertEuro = "Technik.Material.DirektabschreibungBisWertEuro";
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
    public const string KleiderkammerAussonderungVorlaufMonate = "Kleiderkammer.Aussonderung.VorlaufMonate";
    public const string StandortfortbildungStunden = "Ausbildung.Standortfortbildung.StundenJahr";
    public const string BelastungsuebungIntervallMonate = "Atemschutz.Belastungsuebung.IntervallMonate";
    public const string EinsatzuebungIntervallMonate = "Atemschutz.Einsatzuebung.IntervallMonate";
    public const string AtemschutzUnterweisungIntervallMonate = "Atemschutz.Unterweisung.IntervallMonate";
    public const string VorsorgeIntervallMonate = "Atemschutz.Vorsorge.IntervallMonate";
    public const string VorsorgeIntervallAbAlterMonate = "Atemschutz.Vorsorge.IntervallAbAlterMonate";
    public const string VorsorgeKuerzeresIntervallAbAlter = "Atemschutz.Vorsorge.KuerzeresIntervallAbAlter";
    public const string LeistungsabzeichenBelastungsuebungMonate = "Leistungsabzeichen.Belastungsuebung.HoechstensMonate";

    // Gleicher Schlüssel wie im Ehrungen-Teil, dort gepflegt.
    public const string LeistungsspangePflichtAbEintritt = "Ehrung.Jugend.LeistungsspangeAbEintritt";
    public const string LeistungsspangeWarnungTage = "Jugend.Leistungsspange.WarnungTage";

    public const string DienstSerieMaxMonate = "Dienst.Serie.MaxMonate";
    public const string DienstRueckmeldungSchlussStunden = "Dienst.Rueckmeldung.SchlussStunden";
    public const string KalenderVorschauMonate = "Dienst.Kalender.VorschauMonate";
    public const string KalenderRueckblickMonate = "Dienst.Kalender.RueckblickMonate";

    // Löschfristen nach dem Löschkonzept; angewendet vom nächtlichen Löschlauf.
    public const string RueckmeldungBegruendungLoeschenMonate = "Dienst.Rueckmeldung.BegruendungLoeschenMonate";
    public const string RueckmeldungLoeschenMonate = "Dienst.Rueckmeldung.LoeschenMonate";
    public const string LoeschlaufAbStunde = "Datenschutz.Loeschlauf.AbStunde";
    public const string KontoLoeschenNachDeaktivierungMonate = "Datenschutz.Konto.LoeschenNachDeaktivierungMonate";
    public const string KontaktdatenLoeschenNachAustrittMonate = "Datenschutz.Kontaktdaten.LoeschenNachAustrittMonate";
    public const string AnschriftenVerlaufLoeschenNachMonate = "Datenschutz.Anschriften.VerlaufLoeschenNachMonate";
    public const string AntraegeLoeschenNachMonate = "Datenschutz.Antraege.LoeschenNachMonate";
    public const string AntragsentwuerfeLoeschenNachMonate = "Datenschutz.Antragsentwuerfe.LoeschenNachMonate";

    /// <summary>Volljährigkeit (§ 2 BGB): ab dann unterschreibt das Mitglied selbst.</summary>
    public const string VolljaehrigkeitAlter = "Mitglied.Volljaehrigkeit.Alter";

    /// <summary>So lange nach dem 18. Geburtstag erinnert die Personalprüfung an die Erklärung bei Volljährigkeit.</summary>
    public const string VolljaehrigkeitErinnernMonate = "Mitglied.Volljaehrigkeit.ErinnernMonate";

    /// <summary>Anträge höchstens so viele Monate vor dem gewünschten Tag (Andi, 24.10.2026: ein halbes Jahr).</summary>
    public const string AntraegeVorlaufMonate = "Antraege.Vorlauf.Monate";

    // Texte für die Datenschutzhinweise und die Datenschutzinformation der Antragsformulare; bis zur Freigabe Platzhalter.
    // Die verantwortliche Stelle ist die Gemeinde aus den Stammdaten (Organisation).
    public const string DatenschutzBeauftragter = "Datenschutz.Beauftragter";
    public const string FotoKanaele = "Datenschutz.Fotos.Kanaele";
    public const string Art9Grundlage = "Datenschutz.Art9.Grundlage";
    public const string AlarmierungBetreiber = "Datenschutz.Alarmierung.Betreiber";
    public const string Auftragsverarbeiter = "Datenschutz.Auftragsverarbeiter";

    // Fristen aus dem Löschkonzept, die die Datenschutzinformation nennt, bevor der Löschlauf sie umsetzt.
    public const string RestdatensatzAufbewahrungJahre = "Datenschutz.Restdatensatz.AufbewahrungJahre";
    public const string EinsatzberichteAufbewahrungJahre = "Datenschutz.Einsatzberichte.AufbewahrungJahre";

    /// <summary>
    /// Löschkonzept L8: Atemschutznachweise sind das Expositionsverzeichnis (§ 10a GefStoffV, KoAtEx der DGUV) und bleiben so viele Jahre
    /// nach dem letzten Nachweis des Mitglieds erhalten (Festlegung Andi, 25.10.2026: 40 Jahre, keine Übergabe an die ZED).
    /// </summary>
    public const string AtemschutznachweiseAufbewahrungJahre = "Datenschutz.Atemschutznachweise.AufbewahrungJahre";
    public const string PruefhinweiseLoeschenNachMonate = "Datenschutz.Pruefhinweise.LoeschenNachMonate";
    public const string ErziehungsberechtigteLoeschenAbAlter = "Datenschutz.Erziehungsberechtigte.LoeschenAbAlter";
    public const string AenderungsprotokollAufbewahrungMonate = "Datenschutz.Aenderungsprotokoll.AufbewahrungMonate";

    /// <summary>Löschkonzept L19: Im Änderungsprotokoll nach so vielen Monaten die alten und neuen Feldwerte entfernen; wer wann was geändert hat, bleibt bis zur Aufbewahrungsfrist.</summary>
    public const string AenderungsprotokollVerdichtenNachMonate = "Datenschutz.Aenderungsprotokoll.VerdichtenNachMonate";

    /// <summary>Unfallanzeige: so viele Tage nach Kenntnis vom Unfall muss sie bei der Unfallkasse sein (§ 193 Abs. 4 SGB VII).</summary>
    public const string UnfallAnzeigefristTage = "Unfall.AnzeigefristTage";

    /// <summary>Startseite „Was liegt an“: Termine mit offener Rückmeldung so viele Tage im Voraus.</summary>
    public const string StartseiteVorschauTage = "Startseite.VorschauTage";

    /// <summary>Löschkonzept L36: Dienstunfälle samt Zeugen so viele Jahre nach dem Abschluss löschen.</summary>
    public const string UnfaelleLoeschenNachJahren = "Datenschutz.Unfaelle.LoeschenNachJahren";

    /// <summary>Löschkonzept L5: Anwesenheiten eines Kalenderjahres so viele Jahre nach dessen Ende zu Jahressummen je Mitglied verdichten.</summary>
    public const string AnwesenheitenVerdichtenNachJahren = "Datenschutz.Anwesenheiten.VerdichtenNachJahren";

    /// <summary>Löschkonzept L14: zurückgegebene Ausstattung und die zugehörigen Kleiderkammer-Vorgänge so viele Monate nach der Rückgabe löschen.</summary>
    public const string AusstattungLoeschenNachRueckgabeMonate = "Datenschutz.Ausstattung.LoeschenNachRueckgabeMonate";
    public const string ErinnerungslaufAufbewahrungMonate = "Datenschutz.Erinnerungslauf.AufbewahrungMonate";

    /// <summary>1: Beim Programmstart einmal alle Mitglieder prüfen (Beförderung, Ehrung, Übertritt, Funktion); 0: nur bei Änderungen und nachts.</summary>
    public const string PruefungGesamtlaufBeimStart = "Pruefung.GesamtlaufBeimStart";

    /// <summary>Kostenersatz: angefangene Minuten je Fahrzeug und Einsatzkraft aufrunden auf (30 = halbe Stunde, 60 = Stunde, 1 = genau).</summary>
    /// <summary>Löschkonzept L3: Beteiligte und Ansprechpartner so viele Monate nach Ablauf des Einsatzjahres bzw. nach Abschluss des Kostenersatzes löschen.</summary>
    public const string EinsatzbeteiligteLoeschenNachMonate = "Datenschutz.Einsatzbeteiligte.LoeschenNachMonate";

    public const string KostenersatzRundungMinuten = "Kostenersatz.RundungMinuten";

    // Umsatzsteuer der Gemeinde und ihrer Kameradschaftskassen; die Steuerdaten selbst stehen in der Organisation.
    public const string UmsatzsteuerRegelsatzProzent = "Umsatzsteuer.Regelsatz.Prozent";
    public const string UmsatzsteuerErmaessigterSatzProzent = "Umsatzsteuer.ErmaessigterSatz.Prozent";

    /// <summary>§ 19 Abs. 1 UStG: Kleinunternehmer, solange der Umsatz im Vorjahr höchstens so hoch war ...</summary>
    public const string KleinunternehmerVorjahrEuro = "Umsatzsteuer.Kleinunternehmer.VorjahrEuro";

    /// <summary>... und im laufenden Jahr diesen Betrag nicht überschreitet.</summary>
    public const string KleinunternehmerLaufendesJahrEuro = "Umsatzsteuer.Kleinunternehmer.LaufendesJahrEuro";

    /// <summary>
    /// Jahresumsatz der Kameradschaftskasse (Feste, Verkauf), ab dem die Kämmerei sie als eigene wirtschaftliche Tätigkeit
    /// führt und meldet (Angabe Kämmerei Adelberg 25.10.2026: 45.000 €; R 4.1 Abs. 5 KStR, LFV-Ratgeber Kameradschaftskassen 2026).
    /// </summary>
    public const string KameradschaftskasseUmsatzgrenzeEuro = "Umsatzsteuer.Kameradschaftskasse.UmsatzgrenzeEuro";

    /// <summary>Freibetrag der Körperschaftsteuer für den Betrieb gewerblicher Art (§ 24 KStG).</summary>
    public const string KoerperschaftsteuerFreibetragEuro = "Steuer.Koerperschaftsteuer.FreibetragEuro";

    // Entschädigungssätze folgen dem Muster Entschaedigung.{Vorgang}.{Feld}, siehe Entschaedigungssaetze.
}
