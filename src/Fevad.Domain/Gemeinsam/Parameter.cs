namespace Fevad.Domain.Gemeinsam;

/// <summary>
/// Zeitlich versionierter Konfigurationswert (Altersgrenzen, Fristen, Sätze).
/// Fristen aus Gesetz und Satzung ändern sich; sie gehören nicht in den Code.
/// </summary>
public class Parameter : Entitaet
{
    /// <summary>Schlüssel des Parameters, siehe <see cref="ParameterSchluessel"/>, z. B. „Mitglied.Probezeit.Monate“.</summary>
    public required string Schluessel { get; set; }
    /// <summary>Wert als Text; die Leser wandeln ihn in Zahl, Betrag oder Text um.</summary>
    public required string Wert { get; set; }
    /// <summary>Erster Tag, an dem der Wert gilt.</summary>
    public DateOnly GueltigAb { get; set; }
    /// <summary>Letzter Tag, an dem der Wert gilt; leer bedeutet unbefristet.</summary>
    public DateOnly? GueltigBis { get; set; }

    /// <summary>Herkunft, z. B. "Feuerwehrsatzung Adelberg 2019".</summary>
    public string? Quelle { get; set; }

    /// <summary>Prüft, ob der Wert an einem Tag gilt.</summary>
    /// <param name="stichtag">Zu prüfender Tag.</param>
    /// <returns>true, wenn der Stichtag zwischen <see cref="GueltigAb"/> und <see cref="GueltigBis"/> liegt (jeweils einschließlich).</returns>
    public bool IstGueltigAm(DateOnly stichtag) =>
        GueltigAb <= stichtag && (GueltigBis is null || stichtag <= GueltigBis);
}

/// <summary>Bekannte Parameter-Schlüssel.</summary>
public static class ParameterSchluessel
{
    /// <summary>Dauer der Probezeit neuer Mitglieder der Einsatzabteilung in Monaten (Feuerwehrsatzung).</summary>
    public const string ProbezeitMonate = "Mitglied.Probezeit.Monate";
    /// <summary>Alter, mit dem der Dienst in der Einsatzabteilung endet (Feuerwehrsatzung, Standard 65 Jahre).</summary>
    public const string AltersgrenzeEinsatzdienst = "Mitglied.Altersgrenze.Einsatzdienst";
    /// <summary>Alter, ab dem ein Wechsel in die Altersabteilung möglich ist.</summary>
    public const string AltersabteilungAbAlter = "Mitglied.Altersabteilung.AbAlter";
    /// <summary>So viele Tage vor Ablauf werden Personalfristen gelb angezeigt.</summary>
    public const string FristWarnungTage = "Frist.Warnung.Tage";
    /// <summary>So viele Tage vor Fälligkeit werden Geräteprüfungen gelb angezeigt.</summary>
    public const string PruefungWarnungTage = "Technik.Pruefung.WarnungTage";
    /// <summary>Ab diesem Anschaffungswert in Euro wird Material als Einzelstück inventarisiert.</summary>
    public const string InventarisierungAbWertEuro = "Technik.Material.InventarisierungAbWertEuro";
    /// <summary>Bis zu diesem Anschaffungswert in Euro wird Material direkt als Verbrauch abgeschrieben.</summary>
    public const string DirektabschreibungBisWertEuro = "Technik.Material.DirektabschreibungBisWertEuro";
    /// <summary>Allgemeiner Abstand der jährlichen Atemschutzpflichten in Monaten (FwDV 7); gilt, wenn für eine Übungsart kein eigenes Intervall gepflegt ist.</summary>
    public const string AtemschutzUebungIntervallMonate = "Atemschutz.Uebung.IntervallMonate";
    /// <summary>Löschkonzept L1: so viele Monate bleiben die Rohdaten der Alarmierung gespeichert.</summary>
    public const string AlarmdatenAufbewahrungMonate = "Einsatz.Alarmdaten.AufbewahrungMonate";
    /// <summary>Muster für die Einsatznummer, z. B. mit Jahr und laufender Nummer.</summary>
    public const string EinsatznummerFormat = "Einsatz.Nummer.Format";
    /// <summary>Größte zulässige Dateigröße für hochgeladene Dokumente in Megabyte.</summary>
    public const string DokumentMaxMegabyte = "Dokument.Datei.MaxMegabyte";
    /// <summary>Mindestfrist in Tagen zwischen Einladung und Hauptversammlung (Feuerwehrsatzung).</summary>
    public const string HauptversammlungEinladungsfristTage = "Gremien.Hauptversammlung.EinladungsfristTage";
    /// <summary>Mindestfrist in Tagen zwischen Einladung und Sitzung des Feuerwehrausschusses.</summary>
    public const string AusschussEinladungsfristTage = "Gremien.Ausschuss.EinladungsfristTage";
    /// <summary>So viele Tage vor Ende einer Amtszeit weist FEVAD auf die anstehende Wahl hin.</summary>
    public const string AmtszeitWarnungTage = "Gremien.Amtszeit.WarnungTage";
    /// <summary>So viele Tage vor Ablauf werden Ausbildungsnachweise gelb angezeigt.</summary>
    public const string NachweisWarnungTage = "Ausbildung.Nachweis.WarnungTage";
    /// <summary>So viele Tage vor Ablauf werden Atemschutzfristen gelb angezeigt.</summary>
    public const string AtemschutzWarnungTage = "Atemschutz.WarnungTage";
    /// <summary>Mindestabstand in Tagen zwischen zwei Erinnerungsmails zu Fristen.</summary>
    public const string ErinnerungIntervallTage = "Frist.Erinnerung.IntervallTage";
    /// <summary>Stunde (Ortszeit), ab der der tägliche Erinnerungslauf startet.</summary>
    public const string ErinnerungAbStunde = "Frist.Erinnerung.AbStunde";
    /// <summary>So viele Monate vor Ende der Nutzungsdauer erscheint Ausstattung in der Aussonderungsliste.</summary>
    public const string KleiderkammerAussonderungVorlaufMonate = "Kleiderkammer.Aussonderung.VorlaufMonate";
    /// <summary>Stunden Standortfortbildung, die jede Einsatzkraft im Jahr leisten soll.</summary>
    public const string StandortfortbildungStunden = "Ausbildung.Standortfortbildung.StundenJahr";
    /// <summary>Abstand der Belastungsübung für Atemschutzgeräteträger in Monaten (FwDV 7).</summary>
    public const string BelastungsuebungIntervallMonate = "Atemschutz.Belastungsuebung.IntervallMonate";
    /// <summary>Abstand der Einsatzübung für Atemschutzgeräteträger in Monaten (FwDV 7).</summary>
    public const string EinsatzuebungIntervallMonate = "Atemschutz.Einsatzuebung.IntervallMonate";
    /// <summary>Abstand der Atemschutz-Unterweisung in Monaten.</summary>
    public const string AtemschutzUnterweisungIntervallMonate = "Atemschutz.Unterweisung.IntervallMonate";
    /// <summary>Abstand der arbeitsmedizinischen Vorsorge G 26.3 in Monaten bis zur Altersgrenze (nur Status und Frist, keine Befunde).</summary>
    public const string VorsorgeIntervallMonate = "Atemschutz.Vorsorge.IntervallMonate";

    /// <summary>Ab so vielen Minuten unter Atemschutz zählt ein Realeinsatz als Einsatzübung nach FwDV 7 (V11); 0 = jeder Einsatz.</summary>
    public const string EinsatzAlsEinsatzuebungAbMinuten = "Atemschutz.Einsatz.AlsEinsatzuebungAbMinuten";

    /// <summary>So viele Tage darf sich ein Geräteträger selbst als nicht einsetzbar melden; länger nur mit Genehmigung (V62, Andi 25.10.2026).</summary>
    public const string AtemschutzSelbstmeldungHoechstTage = "Atemschutz.Selbstmeldung.HoechstTage";
    /// <summary>Abstand der arbeitsmedizinischen Vorsorge in Monaten ab dem Alter aus <see cref="VorsorgeKuerzeresIntervallAbAlter"/>.</summary>
    public const string VorsorgeIntervallAbAlterMonate = "Atemschutz.Vorsorge.IntervallAbAlterMonate";
    /// <summary>Alter, ab dem die Vorsorge im kürzeren Abstand fällig wird.</summary>
    public const string VorsorgeKuerzeresIntervallAbAlter = "Atemschutz.Vorsorge.KuerzeresIntervallAbAlter";
    /// <summary>So alt darf die letzte Belastungsübung höchstens sein, um zum Leistungsabzeichen zugelassen zu werden.</summary>
    public const string LeistungsabzeichenBelastungsuebungMonate = "Leistungsabzeichen.Belastungsuebung.HoechstensMonate";

    // Gleicher Schlüssel wie im Ehrungen-Teil, dort gepflegt.
    /// <summary>Eintritt in die Jugendfeuerwehr, ab dem die Jugendzeit nur mit Leistungsspange auf das Ehrenzeichen angerechnet wird (VwV Feuerwehr-Ehrenzeichen Nr. 6).</summary>
    public const string LeistungsspangePflichtAbEintritt = "Ehrung.Jugend.LeistungsspangeAbEintritt";
    /// <summary>So viele Tage vor dem Übertrittsalter wird der Hinweis auf eine fehlende Leistungsspange rot statt gelb.</summary>
    public const string LeistungsspangeWarnungTage = "Jugend.Leistungsspange.WarnungTage";

    /// <summary>So viele Monate im Voraus darf eine Dienstserie Termine anlegen.</summary>
    public const string DienstSerieMaxMonate = "Dienst.Serie.MaxMonate";
    /// <summary>So viele Stunden vor Dienstbeginn endet die Zu- oder Absage.</summary>
    public const string DienstRueckmeldungSchlussStunden = "Dienst.Rueckmeldung.SchlussStunden";
    /// <summary>So viele Monate in die Zukunft zeigt der Dienstkalender.</summary>
    public const string KalenderVorschauMonate = "Dienst.Kalender.VorschauMonate";
    /// <summary>So viele Monate in die Vergangenheit zeigt der Dienstkalender.</summary>
    public const string KalenderRueckblickMonate = "Dienst.Kalender.RueckblickMonate";

    // Löschfristen nach dem Löschkonzept; angewendet vom nächtlichen Löschlauf.
    /// <summary>So viele Monate nach dem Dienst wird die Begründung einer Absage gelöscht.</summary>
    public const string RueckmeldungBegruendungLoeschenMonate = "Dienst.Rueckmeldung.BegruendungLoeschenMonate";
    /// <summary>So viele Monate nach dem Dienst werden Zu- und Absagen ganz gelöscht.</summary>
    public const string RueckmeldungLoeschenMonate = "Dienst.Rueckmeldung.LoeschenMonate";
    /// <summary>Stunde (Ortszeit), ab der der nächtliche Löschlauf startet.</summary>
    public const string LoeschlaufAbStunde = "Datenschutz.Loeschlauf.AbStunde";
    /// <summary>So viele Monate nach der Deaktivierung wird ein Benutzerkonto gelöscht.</summary>
    public const string KontoLoeschenNachDeaktivierungMonate = "Datenschutz.Konto.LoeschenNachDeaktivierungMonate";
    /// <summary>So viele Monate nach dem Austritt werden Kontaktdaten des Mitglieds gelöscht.</summary>
    public const string KontaktdatenLoeschenNachAustrittMonate = "Datenschutz.Kontaktdaten.LoeschenNachAustrittMonate";
    /// <summary>Löschkonzept L34: so viele Monate bleiben frühere Anschriften im Verlauf erhalten.</summary>
    public const string AnschriftenVerlaufLoeschenNachMonate = "Datenschutz.Anschriften.VerlaufLoeschenNachMonate";
    /// <summary>So viele Monate nach der Entscheidung werden abgeschlossene Anträge gelöscht.</summary>
    public const string AntraegeLoeschenNachMonate = "Datenschutz.Antraege.LoeschenNachMonate";
    /// <summary>So viele Monate nach der letzten Änderung werden nie eingereichte Antragsentwürfe gelöscht.</summary>
    public const string AntragsentwuerfeLoeschenNachMonate = "Datenschutz.Antragsentwuerfe.LoeschenNachMonate";

    /// <summary>Volljährigkeit (§ 2 BGB): ab dann unterschreibt das Mitglied selbst.</summary>
    public const string VolljaehrigkeitAlter = "Mitglied.Volljaehrigkeit.Alter";

    /// <summary>So lange nach dem 18. Geburtstag erinnert die Personalprüfung an die Erklärung bei Volljährigkeit.</summary>
    public const string VolljaehrigkeitErinnernMonate = "Mitglied.Volljaehrigkeit.ErinnernMonate";

    /// <summary>Anträge höchstens so viele Monate vor dem gewünschten Tag (Andi, 24.10.2026: ein halbes Jahr).</summary>
    public const string AntraegeVorlaufMonate = "Antraege.Vorlauf.Monate";

    // Texte für die Datenschutzhinweise und die Datenschutzinformation der Antragsformulare; bis zur Freigabe Platzhalter.
    // Die verantwortliche Stelle ist die Gemeinde aus den Stammdaten (Organisation).
    /// <summary>Name und Kontakt des Datenschutzbeauftragten für die Datenschutzinformation.</summary>
    public const string DatenschutzBeauftragter = "Datenschutz.Beauftragter";
    /// <summary>Kanäle, in denen die Feuerwehr Fotos veröffentlicht (Einwilligung im Antrag).</summary>
    public const string FotoKanaele = "Datenschutz.Fotos.Kanaele";
    /// <summary>Rechtsgrundlage nach Art. 9 DSGVO für Gesundheitsdaten (Atemschutzvorsorge).</summary>
    public const string Art9Grundlage = "Datenschutz.Art9.Grundlage";
    /// <summary>Betreiber der Alarmierung, der Daten an FEVAD übergibt.</summary>
    public const string AlarmierungBetreiber = "Datenschutz.Alarmierung.Betreiber";
    /// <summary>Auftragsverarbeiter (Hosting, Mail), die die Datenschutzinformation nennt.</summary>
    public const string Auftragsverarbeiter = "Datenschutz.Auftragsverarbeiter";

    // Fristen aus dem Löschkonzept, die die Datenschutzinformation nennt, bevor der Löschlauf sie umsetzt.
    /// <summary>Löschkonzept: so viele Jahre nach dem Austritt bleibt der Restdatensatz eines Mitglieds erhalten.</summary>
    public const string RestdatensatzAufbewahrungJahre = "Datenschutz.Restdatensatz.AufbewahrungJahre";
    /// <summary>Löschkonzept: so viele Jahre bleiben Einsatzberichte erhalten.</summary>
    public const string EinsatzberichteAufbewahrungJahre = "Datenschutz.Einsatzberichte.AufbewahrungJahre";

    /// <summary>
    /// Löschkonzept L8: Atemschutznachweise sind das Expositionsverzeichnis (§ 10a GefStoffV, KoAtEx der DGUV) und bleiben so viele Jahre
    /// nach dem letzten Nachweis des Mitglieds erhalten (Festlegung Andi, 25.10.2026: 40 Jahre, keine Übergabe an die ZED).
    /// </summary>
    public const string AtemschutznachweiseAufbewahrungJahre = "Datenschutz.Atemschutznachweise.AufbewahrungJahre";
    /// <summary>So viele Monate nach der Erledigung werden Prüfhinweise (Beförderung, Ehrung, Übertritt) gelöscht.</summary>
    public const string PruefhinweiseLoeschenNachMonate = "Datenschutz.Pruefhinweise.LoeschenNachMonate";
    /// <summary>Ab diesem Alter des Mitglieds werden die Angaben der Erziehungsberechtigten gelöscht.</summary>
    public const string ErziehungsberechtigteLoeschenAbAlter = "Datenschutz.Erziehungsberechtigte.LoeschenAbAlter";
    /// <summary>So viele Monate bleiben Einträge im Änderungsprotokoll erhalten.</summary>
    public const string AenderungsprotokollAufbewahrungMonate = "Datenschutz.Aenderungsprotokoll.AufbewahrungMonate";

    /// <summary>Löschkonzept L19: Im Änderungsprotokoll nach so vielen Monaten die alten und neuen Feldwerte entfernen; wer wann was geändert hat, bleibt bis zur Aufbewahrungsfrist.</summary>
    public const string AenderungsprotokollVerdichtenNachMonate = "Datenschutz.Aenderungsprotokoll.VerdichtenNachMonate";

    /// <summary>Unfallanzeige: so viele Tage nach Kenntnis vom Unfall muss sie bei der Unfallkasse sein (§ 193 Abs. 4 SGB VII).</summary>
    public const string UnfallAnzeigefristTage = "Unfall.AnzeigefristTage";

    /// <summary>Startseite „Was liegt an“: Termine mit offener Rückmeldung so viele Tage im Voraus.</summary>
    public const string StartseiteVorschauTage = "Startseite.VorschauTage";

    /// <summary>Löschkonzept L37: Bankverbindungen so viele Monate nach dem Austritt löschen (Zeit für die letzte Auszahlung).</summary>
    public const string BankverbindungLoeschenNachMonate = "Datenschutz.Bankverbindung.LoeschenNachAustrittMonate";

    /// <summary>Löschkonzept L38: Abmeldungen „vorübergehend nicht einsetzbar“ so viele Monate nach Ende oder Aufhebung löschen.</summary>
    public const string AtemschutzSperrenLoeschenNachMonate = "Datenschutz.AtemschutzSperren.LoeschenNachMonate";

    /// <summary>Löschkonzept L36: Dienstunfälle samt Zeugen so viele Jahre nach dem Abschluss löschen.</summary>
    public const string UnfaelleLoeschenNachJahren = "Datenschutz.Unfaelle.LoeschenNachJahren";

    /// <summary>Löschkonzept L5: Anwesenheiten eines Kalenderjahres so viele Jahre nach dessen Ende zu Jahressummen je Mitglied verdichten.</summary>
    public const string AnwesenheitenVerdichtenNachJahren = "Datenschutz.Anwesenheiten.VerdichtenNachJahren";

    /// <summary>Löschkonzept L14: zurückgegebene Ausstattung und die zugehörigen Kleiderkammer-Vorgänge so viele Monate nach der Rückgabe löschen.</summary>
    public const string AusstattungLoeschenNachRueckgabeMonate = "Datenschutz.Ausstattung.LoeschenNachRueckgabeMonate";
    /// <summary>So viele Monate bleibt das Protokoll der Erinnerungsläufe erhalten.</summary>
    public const string ErinnerungslaufAufbewahrungMonate = "Datenschutz.Erinnerungslauf.AufbewahrungMonate";

    /// <summary>1: Beim Programmstart einmal alle Mitglieder prüfen (Beförderung, Ehrung, Übertritt, Funktion); 0: nur bei Änderungen und nachts.</summary>
    public const string PruefungGesamtlaufBeimStart = "Pruefung.GesamtlaufBeimStart";

    /// <summary>Löschkonzept L3: Beteiligte und Ansprechpartner so viele Monate nach Ablauf des Einsatzjahres bzw. nach Abschluss des Kostenersatzes löschen.</summary>
    public const string EinsatzbeteiligteLoeschenNachMonate = "Datenschutz.Einsatzbeteiligte.LoeschenNachMonate";

    /// <summary>Kostenersatz: angefangene Minuten je Fahrzeug und Einsatzkraft aufrunden auf (30 = halbe Stunde, 60 = Stunde, 1 = genau).</summary>
    public const string KostenersatzRundungMinuten = "Kostenersatz.RundungMinuten";

    // Umsatzsteuer der Gemeinde und ihrer Kameradschaftskassen; die Steuerdaten selbst stehen in der Organisation.
    /// <summary>Regelsteuersatz der Umsatzsteuer in Prozent (§ 12 Abs. 1 UStG).</summary>
    public const string UmsatzsteuerRegelsatzProzent = "Umsatzsteuer.Regelsatz.Prozent";
    /// <summary>Ermäßigter Steuersatz der Umsatzsteuer in Prozent (§ 12 Abs. 2 UStG).</summary>
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
