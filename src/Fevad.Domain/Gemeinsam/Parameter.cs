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

    // Prüfplakette (V58): Etikettendrucker mit einem Etikett je Seite oder A4-Bogen mit Spalten und Zeilen.
    /// <summary>Breite eines Etiketts im Etikettendrucker in Millimetern.</summary>
    public const string EtikettBreiteMm = "Technik.Etikett.BreiteMm";
    /// <summary>Höhe eines Etiketts im Etikettendrucker in Millimetern.</summary>
    public const string EtikettHoeheMm = "Technik.Etikett.HoeheMm";
    /// <summary>Spalten auf dem A4-Etikettenbogen.</summary>
    public const string EtikettBogenSpalten = "Technik.Etikett.BogenSpalten";
    /// <summary>Zeilen auf dem A4-Etikettenbogen.</summary>
    public const string EtikettBogenZeilen = "Technik.Etikett.BogenZeilen";
    /// <summary>Ab diesem Anschaffungswert in Euro braucht die Aussonderung eines Geräts oder Fahrzeugs einen Beschluss (V60); nur Hinweis, keine Sperre.</summary>
    public const string AussonderungBeschlussAbWertEuro = "Technik.Aussonderung.BeschlussAbWertEuro";
    /// <summary>Pauschalförderung des Landes je Angehörigem der Einsatzabteilung zum 31.12. in Euro (VwV Z-Feu, V28).</summary>
    public const string PauschalfoerderungJeAngehoerigemEuro = "Foerderung.Pauschale.JeAngehoerigemEuro";
    /// <summary>Jährlicher Zuschuss der Gemeinde an die Kameradschaftskasse in Euro (Entschädigungssatzung § 5 Abs. 1).</summary>
    public const string KameradschaftskasseZuschussEuro = "Kameradschaftskasse.Zuschuss.AllgemeinEuro";
    /// <summary>Jährlicher Zuschuss der Gemeinde zur Förderung der Jugendfeuerwehr in Euro (Entschädigungssatzung § 5 Abs. 2).</summary>
    public const string KameradschaftskasseZuschussJugendEuro = "Kameradschaftskasse.Zuschuss.JugendEuro";
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
    /// <summary>
    /// Jährliche Atemschutzpflichten zählen im Kalenderjahr und werden ab dem Ersten dieses Monats gelb, wenn im laufenden Jahr noch
    /// kein Nachweis vorliegt (Andi: Warnung ab 01.10.). 0 = gleitend ab dem letzten Nachweis mit <see cref="AtemschutzWarnungTage"/>.
    /// </summary>
    public const string AtemschutzJahrespflichtWarnungAbMonat = "Atemschutz.Jahrespflicht.WarnungAbMonat";
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
    /// <summary>So viele Bewerber bilden eine Gruppe für die Leistungsspange (DJF-Richtlinie: neun).</summary>
    public const string LeistungsspangeGruppenstaerke = "Jugend.Leistungsspange.Gruppenstaerke";

    /// <summary>So viele Monate im Voraus darf eine Dienstserie Termine anlegen.</summary>
    public const string DienstSerieMaxMonate = "Dienst.Serie.MaxMonate";
    /// <summary>So viele Stunden vor Dienstbeginn endet die Zu- oder Absage.</summary>
    public const string DienstRueckmeldungSchlussStunden = "Dienst.Rueckmeldung.SchlussStunden";
    /// <summary>So viele Jahre bleiben Verbandbucheinträge und Beinaheunfälle ab dem Ereignis (DGUV Vorschrift 1 § 24 Abs. 6: fünf Jahre), L36.</summary>
    public const string VerbandbuchAufbewahrungJahre = "Unfall.Verbandbuch.AufbewahrungJahre";
    /// <summary>So viele Jahre nach Erstattung oder Ablehnung wird ein Sachschaden gelöscht (L40).</summary>
    public const string SachschadenLoeschenNachJahren = "Sachschaden.LoeschenNachJahren";
    /// <summary>Höchstens so viele Fotos je Unfall- oder Schadensmeldung.</summary>
    public const string MeldungFotosHoechstAnzahl = "Meldung.Fotos.HoechstAnzahl";
    /// <summary>Höchstgröße eines Fotos in Megabyte.</summary>
    public const string MeldungFotoHoechstMegabyte = "Meldung.Foto.HoechstMegabyte";
    /// <summary>So viele Tage nach dem Diensttag kann sich ein Mitglied noch selbst entschuldigen (Satzung § 5 Abs. 6: bis zum Folgetag).</summary>
    public const string DienstEntschuldigungNachfristTage = "Dienst.Entschuldigung.NachfristTage";
    /// <summary>Ab so vielen Tagen ist eine Abwesenheit dem Kommandanten anzuzeigen (Satzung § 5 Abs. 6); 0 schaltet die Anzeige ab.</summary>
    public const string AbwesenheitAnzeigeAbTagen = "Dienst.Abwesenheit.AnzeigeAbTagen";
    /// <summary>So viele Monate nach ihrem Ende wird eine gemeldete Abwesenheit gelöscht (L39); die Anwesenheit „entschuldigt“ bleibt.</summary>
    public const string AbwesenheitLoeschenNachMonaten = "Dienst.Abwesenheit.LoeschenNachMonaten";
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
    /// <summary>Ab diesem Alter müssen Jugendliche einer Einwilligung ihrer Eltern selbst zustimmen (V45, Rückfrage B: 16).</summary>
    public const string EinwilligungMitentscheidungAbAlter = "Jugend.Einwilligung.MitentscheidungAbAlter";
    /// <summary>Die Notfallmappe der Jugend- und Kinderfeuerwehr nach so vielen Monaten von den Eltern erneuern lassen (V46).</summary>
    public const string NotfallmappeErneuernMonate = "Jugend.Notfallmappe.ErneuernMonate";
    /// <summary>Erweitertes Führungszeugnis in der Jugendfeuerwehr nach so vielen Jahren erneut vorlegen (V47, Vereinbarung Landkreis Göppingen: 5).</summary>
    public const string FuehrungszeugnisWiedervorlageJahre = "Jugend.Fuehrungszeugnis.WiedervorlageJahre";
    /// <summary>Wiedervorlage des Führungszeugnisses in der Kinderfeuerwehr in Jahren (V47: 2).</summary>
    public const string FuehrungszeugnisWiedervorlageJahreKinder = "Jugend.Fuehrungszeugnis.WiedervorlageJahreKinder";
    /// <summary>Das Führungszeugnis darf bei der Einsicht höchstens so viele Monate alt sein (V47: 3).</summary>
    public const string FuehrungszeugnisHoechstalterMonate = "Jugend.Fuehrungszeugnis.HoechstalterMonate";
    /// <summary>So viele Tage vor der Wiedervorlage erscheint das Zeugnis unter „Was liegt an“.</summary>
    public const string FuehrungszeugnisVorlaufTage = "Jugend.Fuehrungszeugnis.VorlaufTage";
    /// <summary>Nächster Prüftermin des Schutzkonzepts der Jugend- und Kinderfeuerwehr als Datum „JJJJ-MM-TT“ (F26); ohne Wert ist keines beschlossen.</summary>
    public const string SchutzkonzeptPruefungAm = "Jugend.Schutzkonzept.PruefungAm";
    /// <summary>Nach einer Prüfung ist das Schutzkonzept nach so vielen Monaten erneut zu prüfen (F26, Muster: 24).</summary>
    public const string SchutzkonzeptPruefIntervallMonate = "Jugend.Schutzkonzept.PruefIntervallMonate";
    /// <summary>So viele Tage vor dem Prüftermin erinnert „Was liegt an“ (F26, Muster: 28).</summary>
    public const string SchutzkonzeptVorlaufTage = "Jugend.Schutzkonzept.VorlaufTage";
    /// <summary>Löschkonzept L53: Betreuer-Eintrag samt Angaben zum Führungszeugnis so viele Monate nach Ende der Tätigkeit löschen (§ 72a Abs. 5 SGB VIII).</summary>
    public const string BetreuerLoeschenNachMonate = "Datenschutz.Betreuer.LoeschenNachMonate";
    /// <summary>
    /// Löschkonzept L55: Anmeldungen und Betreuer einer Jugendveranstaltung so viele Monate nach dem letzten Tag löschen; verlangt der
    /// Zuschussgeber eine längere Aufbewahrung der Teilnehmerliste, gilt seine.
    /// </summary>
    public const string JugendveranstaltungenLoeschenNachMonate = "Datenschutz.Jugendveranstaltungen.LoeschenNachMonate";
    /// <summary>Löschkonzept L56: Einträge der Warteliste so viele Monate nach Wiedervorlage oder Abschluss löschen (V50).</summary>
    public const string WartelisteLoeschenNachMonate = "Datenschutz.Warteliste.LoeschenNachMonate";
    /// <summary>Löschkonzept L51: widerrufene und abgelehnte Einwilligungen so viele Monate aufbewahren, alle Einträge so lange nach dem Austritt.</summary>
    public const string EinwilligungenAufbewahrungMonate = "Datenschutz.Einwilligungen.AufbewahrungMonate";
    /// <summary>Löschkonzept L52: erledigte Löschanfragen so viele Monate als Nachweis aufbewahren.</summary>
    public const string LoeschanfragenAufbewahrungMonate = "Datenschutz.Loeschanfragen.AufbewahrungMonate";
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

    /// <summary>Zuständiges Archiv, dem Unterlagen vor dem Löschen angeboten werden (V65, F5: Gemeindearchiv Adelberg).</summary>
    public const string ArchivStelle = "Archiv.Stelle";
    /// <summary>So viele Monate nach dem Anbieten darf gelöscht werden, wenn das Archiv nicht entschieden hat (V65).</summary>
    public const string ArchivEntscheidungFristMonate = "Archiv.EntscheidungFristMonate";
    /// <summary>
    /// Bewertungskatalog (V65): Präfix je <c>Archivgut</c>, z. B. „Archiv.Bewertung.Mitgliederakte“. Wert „archivwürdig“ heißt anbieten,
    /// „kassabel“ heißt: das Archiv hat die Art pauschal als nicht archivwürdig bewertet, der Löschlauf löscht ohne Einzelangebot.
    /// </summary>
    public const string ArchivBewertungPraefix = "Archiv.Bewertung.";

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

    /// <summary>Löschkonzept L13: abgeschlossene Entschädigungsabrechnungen samt Posten und abgerechneten Anträgen so viele Jahre nach dem Ende des Abrechnungsjahrs löschen.</summary>
    public const string AbrechnungenAufbewahrungJahre = "Datenschutz.Abrechnungen.AufbewahrungJahre";

    /// <summary>Löschkonzept L44: Belegdateien abgerechneter Entschädigungsanträge so viele Monate nach dem Abschluss der Abrechnung löschen (die Gemeindekasse hält die Belege).</summary>
    public const string EntschaedigungsantraegeBelegeLoeschenNachMonaten = "Datenschutz.Entschaedigungsantraege.BelegeLoeschenNachMonaten";

    /// <summary>Löschkonzept L44: abgelehnte und zurückgezogene Entschädigungsanträge samt Belegen so viele Monate nach der Entscheidung löschen.</summary>
    public const string EntschaedigungsantraegeAbgelehntLoeschenNachMonaten = "Datenschutz.Entschaedigungsantraege.AbgelehntLoeschenNachMonaten";

    /// <summary>Löschkonzept L45: Arbeitgeberbescheinigungen so viele Jahre nach dem Ende des Ausstellungsjahrs löschen; danach kennt die Prüfseite den Code nicht mehr.</summary>
    public const string ArbeitgeberbescheinigungenAufbewahrungJahre = "Datenschutz.Arbeitgeberbescheinigungen.AufbewahrungJahre";

    /// <summary>Löschkonzept L46: Erstattungen an Arbeitgeber so viele Jahre nach dem Ende des Entscheidungsjahrs löschen.</summary>
    public const string ArbeitgebererstattungenAufbewahrungJahre = "Datenschutz.Arbeitgebererstattungen.AufbewahrungJahre";

    /// <summary>Löschkonzept L37: Bankverbindungen so viele Monate nach dem Austritt löschen (Zeit für die letzte Auszahlung).</summary>
    public const string BankverbindungLoeschenNachMonate = "Datenschutz.Bankverbindung.LoeschenNachAustrittMonate";

    /// <summary>Löschkonzept L38: Abmeldungen „vorübergehend nicht einsetzbar“ so viele Monate nach Ende oder Aufhebung löschen.</summary>
    public const string AtemschutzSperrenLoeschenNachMonate = "Datenschutz.AtemschutzSperren.LoeschenNachMonate";

    /// <summary>Löschkonzept L41: hochgeladene Importdateien, die nicht übernommen wurden, so viele Tage nach der letzten Änderung verwerfen.</summary>
    public const string ImportdateienLoeschenNachTagen = "Datenschutz.Importdateien.LoeschenNachTagen";

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
    /// <summary>Löschkonzept L47: abgeschaltete BMA-Objekte so viele Monate nach der Abschaltung und dem Abschluss aller Kostenersatz-Vorgänge löschen.</summary>
    public const string BmaObjekteLoeschenNachMonaten = "Datenschutz.BmaObjekte.LoeschenNachMonaten";
    /// <summary>Löschkonzept L48: bei Gemeinde-Vorgängen mit Mitgliedsbezug Freitexte und Verweis so viele Jahre nach der Entscheidung entfernen.</summary>
    public const string GemeindevorgaengePersonenbezugLoeschenNachJahren = "Datenschutz.Gemeindevorgaenge.PersonenbezugLoeschenNachJahren";
    /// <summary>Löschkonzept L49: Ablehnungen der Aufnahme so viele Monate nach der Bekanntgabe löschen, eingestellte Verfahren nach der letzten Änderung.</summary>
    public const string MitgliederbescheideAblehnungLoeschenNachMonaten = "Datenschutz.Mitgliederbescheide.AblehnungLoeschenNachMonaten";
    /// <summary>Löschkonzept L49: übrige Bescheide an Mitglieder so viele Jahre nach Bestandskraft löschen.</summary>
    public const string MitgliederbescheideLoeschenNachJahren = "Datenschutz.Mitgliederbescheide.LoeschenNachJahren";
    /// <summary>Löschkonzept L50: Dienstausweise so viele Monate nach Rückgabe bzw. nach Ablauf der Gültigkeit löschen.</summary>
    public const string DienstausweiseLoeschenNachMonaten = "Datenschutz.Dienstausweise.LoeschenNachMonaten";
    /// <summary>Löschkonzept L58: Auftraggeber und Ansprechpartner einer Brandsicherheitswache so viele Monate nach Ende des Jahres der Wache leeren.</summary>
    public const string BrandsicherheitswachenAuftraggeberLoeschenNachMonaten = "Datenschutz.Brandsicherheitswachen.AuftraggeberLoeschenNachMonaten";

    /// <summary>Kostenersatz: angefangene Minuten je Fahrzeug und Einsatzkraft aufrunden auf (30 = halbe Stunde, 60 = Stunde, 1 = genau).</summary>
    public const string KostenersatzRundungMinuten = "Kostenersatz.RundungMinuten";

    /// <summary>Kostenersatz: Vorhaltezuschlag auf Verbrauchsmaterial zu Wiederbeschaffungskosten in Prozent.</summary>
    public const string KostenersatzVorhaltezuschlagProzent = "Kostenersatz.VorhaltezuschlagProzent";

    // Kostenersatzbescheid (V22): Fristen und Textbausteine; die Texte pflegt das Hauptamt unter Kostenersatz → Bescheidtexte.
    /// <summary>Kostenersatzbescheid: Frist der Anhörung in Tagen ab Versand.</summary>
    public const string KostenersatzAnhoerungFristTage = "Kostenersatz.Bescheid.AnhoerungFristTage";
    /// <summary>Kostenersatzbescheid: Zugangsfiktion bei einfachem Brief in Tagen nach Aufgabe zur Post (§ 41 Abs. 2 LVwVfG).</summary>
    public const string KostenersatzZugangsfiktionTage = "Kostenersatz.Bescheid.ZugangsfiktionTage";
    /// <summary>Kostenersatzbescheid: Fälligkeit so viele Monate nach Bekanntgabe (Kostenersatzsatzung).</summary>
    public const string KostenersatzFaelligkeitMonate = "Kostenersatz.Bescheid.FaelligkeitMonate";
    /// <summary>Kostenersatzbescheid: Zahlungsverjährung in Jahren ab Ende des Jahres der Fälligkeit (KAG i. V. m. § 228 AO); steuert die Wiedervorlage.</summary>
    public const string KostenersatzVerjaehrungJahre = "Kostenersatz.Bescheid.VerjaehrungJahre";
    /// <summary>Textbaustein: Anschreiben zur Anhörung.</summary>
    public const string KostenersatzTextAnhoerung = "Kostenersatz.Text.Anhoerung";
    /// <summary>Textbaustein: Begründung des Bescheids.</summary>
    public const string KostenersatzTextBegruendung = "Kostenersatz.Text.Begruendung";
    /// <summary>Textbaustein: Zahlungshinweis mit Bankverbindung der Gemeindekasse.</summary>
    public const string KostenersatzTextZahlung = "Kostenersatz.Text.Zahlung";
    /// <summary>Textbaustein: Rechtsbehelfsbelehrung.</summary>
    public const string KostenersatzTextRechtsbehelf = "Kostenersatz.Text.Rechtsbehelf";

    // Kalkulationshilfe Personalstundensatz (V24).
    /// <summary>Kalkulation des Stundensatzes: Stunden je Angehörigem der Einsatzabteilung und Jahr (§ 34 Abs. 5 FwG).</summary>
    public const string KostenersatzKalkulationStunden = "Kostenersatz.Kalkulation.StundenJeAngehoerigem";
    /// <summary>Kalkulation des Stundensatzes: Wiedervorlage so viele Jahre nach der letzten Kalkulation.</summary>
    public const string KostenersatzKalkulationWiedervorlageJahre = "Kostenersatz.Kalkulation.WiedervorlageJahre";

    // Gemeinde-Vorgänge, Bescheide an Mitglieder und Dienstausweis (V27).
    /// <summary>Höchstbetrag der Geldbuße bei groben Verstößen gegen die Dienstpflichten (Feuerwehrsatzung § 5 Abs. 9).</summary>
    public const string GeldbusseHoechstbetrag = "Gemeinde.Geldbusse.Hoechstbetrag";
    /// <summary>Frist zur Äußerung bei der Anhörung vor einem Bescheid an ein Mitglied, in Tagen ab Versand.</summary>
    public const string MitgliederbescheidAnhoerungFristTage = "Gemeinde.Mitgliederbescheid.AnhoerungFristTage";
    /// <summary>Rechtsbehelfsbelehrung für Bescheide des Bürgermeisters an Mitglieder.</summary>
    public const string MitgliederbescheidTextRechtsbehelf = "Gemeinde.Text.RechtsbehelfMitglied";
    /// <summary>Gültigkeit eines neuen Dienstausweises in Jahren.</summary>
    public const string DienstausweisGueltigkeitJahre = "Gemeinde.Dienstausweis.GueltigkeitJahre";

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

    // Kassen (P2): Kameradschaftskasse und Kasse der Jugendfeuerwehr.
    /// <summary>
    /// Ausgaben über diesem Betrag in Euro sind Sache des Feuerwehrausschusses (Andi, 25.10.2026); FEVAD weist beim Buchen darauf hin
    /// und bietet den Verweis auf den Beschluss an, sperrt aber nichts. 0 schaltet den Hinweis ab.
    /// </summary>
    public const string KasseAusschussbeschlussUeberEuro = "Kasse.Ausschussbeschluss.UeberEuro";

    /// <summary>Größte zulässige Dateigröße eines Kassenbelegs in Megabyte.</summary>
    public const string KasseBelegMaxMegabyte = "Kasse.Beleg.MaxMegabyte";

    /// <summary>Die Steuerampel wird gelb, sobald der Nettoumsatz der wirtschaftlichen Tätigkeit diesen Anteil der Umsatzgrenze erreicht (V03).</summary>
    public const string KasseSteuerWarnungProzent = "Kasse.Steuer.WarnungProzent";

    /// <summary>
    /// Ehrenamtspauschale nach § 3 Nr. 26a EStG in Euro je Jahr. Die Jahresliste der Auslagenerstattungen weist auf Personen hin,
    /// denen eine Kasse im Jahr mehr gezahlt hat (V07); belegte Auslagen bleiben steuerfrei, pauschale Zahlungen nicht.
    /// </summary>
    public const string EhrenamtspauschaleEuro = "Steuer.Ehrenamtspauschale.Euro";

    /// <summary>
    /// Wertgrenze des Bestandsverzeichnisses in Euro (V04, Satzung § 12 Abs. 3): Ausgaben ab diesem Betrag schlägt FEVAD zur Aufnahme
    /// ins Bestandsverzeichnis vor. 0 schaltet die Vorschläge ab.
    /// </summary>
    public const string KasseBestandsverzeichnisAbWertEuro = "Kasse.Bestandsverzeichnis.AbWertEuro";

    /// <summary>
    /// Löschkonzept L42: so viele Jahre nach dem Ende des Kalenderjahrs, in dem ein Kassenjahr abgeschlossen wurde, bleiben dessen Buchungen,
    /// Belege, Kontoumsätze und Auslagenerstattungen erhalten (§ 147 AO). Danach löscht der Löschlauf sie und trägt die Bestände vor.
    /// </summary>
    public const string KassendatenAufbewahrungJahre = "Kasse.Aufbewahrung.Jahre";

    /// <summary>
    /// Spenden und Sponsoring ab diesem Wert in Euro brauchen den Annahmebeschluss des Gemeinderats (§ 78 Abs. 4 GemO, V08); FEVAD
    /// zeigt fehlende Beschlüsse an. 0 heißt: jede Zuwendung. Delegiert die Hauptsatzung kleine Beträge an den Bürgermeister, hier die Grenze eintragen.
    /// </summary>
    public const string KasseZuwendungAnnahmeAbEuro = "Kasse.Zuwendung.AnnahmeAbEuro";

    /// <summary>Löschkonzept L43: so viele Jahre nach dem Ende des Eingangsjahrs bleiben Einträge im Spendenregister samt Geberdaten erhalten.</summary>
    public const string KasseZuwendungAufbewahrungJahre = "Kasse.Zuwendung.Aufbewahrung.Jahre";

    /// <summary>So viele Monate vor dem Ende der Amtszeit eines Rechnungsprüfers weist die Kassenübersicht auf die Neuwahl hin (V09).</summary>
    public const string KassePrueferHinweisMonate = "Kasse.Pruefer.HinweisMonate";

    /// <summary>Haushaltsstelle der Entschädigung (Produkt, Sachkonto) für das Summenblatt zur Kassenanordnung; liefert die Kämmerei.</summary>
    public const string KassenanordnungEntschaedigungBuchungsstelle = "Kassenanordnung.Entschaedigung.Buchungsstelle";

    /// <summary>Satzung, nach der die Entschädigung abgerechnet wird; steht auf dem Summenblatt.</summary>
    public const string EntschaedigungssatzungBezeichnung = "Kassenanordnung.Entschaedigung.Satzung";

    /// <summary>Steuerfreier Mindestbetrag für Aufwandsentschädigungen aus öffentlichen Kassen in Euro je Monat (§ 3 Nr. 12 Satz 2 EStG, R 3.12 Abs. 3 LStR); Hinweis in der Jahresbescheinigung.</summary>
    public const string SteuerAufwandsentschaedigungMonatEuro = "Steuer.Aufwandsentschaedigung.MonatEuro";

    /// <summary>Übungsleiterfreibetrag nach § 3 Nr. 26 EStG in Euro je Jahr; Hinweis in der Jahresbescheinigung.</summary>
    public const string SteuerUebungsleiterEuro = "Steuer.Uebungsleiter.Euro";

    /// <summary>Freigrenze für sonstige Leistungen nach § 22 Nr. 3 EStG in Euro je Jahr; Hinweis in der Jahresbescheinigung.</summary>
    public const string SteuerSonstigeLeistungenFreigrenzeEuro = "Steuer.SonstigeLeistungen.FreigrenzeEuro";

    /// <summary>Ab diesem Monat des Jahres werden offene Pflichtthemen der Unterweisung gelb angezeigt (V53; wie die Atemschutz-Warnung ab 01.10., F27).</summary>
    public const string UnterweisungWarnenAbMonat = "Dienste.Unterweisung.WarnenAbMonat";

    /// <summary>Anteil der Kameradschaftskasse am Kostenersatz einer Brandsicherheitswache in Prozent; nur Vorschlag im Leistungsnachweis (V25, Standard 0).</summary>
    public const string BrandsicherheitswacheAnteilKasseProzent = "Dienste.Brandsicherheitswache.AnteilKasseProzent";

    /// <summary>Sollstunden der Standortausbildung nach Truppmann Teil 1 bis Truppmann Teil 2 (FwDV 2, V53).</summary>
    public const string Truppmann2Stunden = "Ausbildung.Truppmann2.Stunden";

    /// <summary>Zeitraum in Monaten ab Truppmann Teil 1, in dem die Sollstunden für Truppmann Teil 2 zu leisten sind (FwDV 2, V53).</summary>
    public const string Truppmann2Monate = "Ausbildung.Truppmann2.Monate";

    /// <summary>Tage vor dem Termin, ab denen eine Maßnahme aus der Nachbesprechung gelb angezeigt wird (V17).</summary>
    public const string MassnahmeWarnTage = "Einsaetze.Massnahmen.WarnTage";

    // Entschädigungssätze folgen dem Muster Entschaedigung.{Vorgang}.{Feld}, siehe Entschaedigungssaetze.
}
