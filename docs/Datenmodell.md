# Datenmodell-Entwurf FEVAD (Verwaltungssoftware FF Adelberg)

Stand: 22.10.2026, Entwurf 1. Grundlage: Recherchebericht, Technologieentscheidung ASP.NET Core, Cloud-Modell mit Notbetrieb (Recherche im Projektordner).

Scope: Verwaltung und Dokumentation. **Keine** Alarmierung, keine Einsatzführung, keine Verfügbarkeitsanzeige (bleibt in aPager PRO). Einsatzdaten aus Leitstelle/alamos werden nur **nachträglich übernommen**.

---

## 1. Leitlinien für das Modell

| Nr. | Leitlinie | Warum |
|---|---|---|
| L1 | **Primärschlüssel `Guid` (UUID v7)**, nicht `int` | Der Notbetrieb im Gerätehaus legt Datensätze offline an. Mit UUIDs gibt es beim Hochladen keine Schlüsselkonflikte. UUID v7 ist zeitlich sortiert und damit indexfreundlich (`Guid.CreateVersion7()` ab .NET 9). |
| L2 | **Fachliche Nummern getrennt vom Schlüssel** | Einsatznummer `2026-017`, Inventarnummer, Personalnummer sind lesbare Felder mit eigenem Unique-Index. Offline-Einsätze bekommen eine vorläufige Nummer `N-…` und erst in der Cloud die endgültige. |
| L3 | **Historisieren statt überschreiben** | Mitgliedschaft, Abteilung, Dienstgrad, Funktion sind Zeiträume (`GueltigAb`, `GueltigBis`). Daraus berechnen sich Dienstjahre (Ehrenzeichen), Personalstand zum 31.12. und Amtszeitende. |
| L4 | **Fristen und Sätze sind Stammdaten, nie Konstanten** | Altersgrenze 65 (Debatte 67), AMR 14.2 neu gefasst 06/2026, Prüfintervalle DGUV 305-002, Entschädigungssätze je Satzung. Eigene Tabelle `Parameter` mit Gültigkeitszeitraum. |
| L5 | **Gesundheitsdaten minimal** (Art. 9 DSGVO) | Atemschutz-Vorsorge speichert nur Datum, Ergebnis-Status (geeignet / befristet / nicht geeignet) und nächste Frist, **nie den Befund**. Eigene Tabelle, eigene Policy. |
| L6 | **Audit für alles Personenbezogene** | EF-Core-Interceptor schreibt jede Änderung (wer, wann, Tabelle, Schlüssel, alte/neue Werte als JSONB) in `AuditEintrag`. Felder `ErstelltAm/Von`, `GeaendertAm/Von` an jeder Entität. |
| L7 | **Soft-Delete und Löschfristen** | `GeloeschtAm` statt physischem Löschen; der nächtliche Löschlauf (`Loeschlauf`, Hintergrunddienst) löscht oder anonymisiert nach Löschkonzept, Fristen als Parameter `Datenschutz.*`, Nachweis in `Loeschnachweis`. |
| L8 | **Zeitstempel als `timestamptz` in UTC** (`DateTimeOffset`), Anzeige in Europe/Berlin, 24-h-Format | Sommerzeitwechsel bei nächtlichen Einsätzen. Reine Datumswerte (Geburtstag, Prüfdatum) als `DateOnly`. |
| L9 | **Kataloge als Tabellen** mit `Code`, `Bezeichnung`, `Sortierung`, `Aktiv` | Abteilung, Dienstgrad, Funktion, Lehrgangstyp, Qualifikation, Geräteart, Statistik-Kategorie. Ein Seed liefert die BW-Standardwerte. |
| L10 | **Notbetrieb nur anhängend** | Die drei Notbetriebs-Formulare (Anwesenheit, Einsatz-Entwurf, Prüfprotokoll) erzeugen nur neue Datensätze, ändern nie bestehende. Dadurch keine Konflikte. |

Namen im Code sind **deutsch** (Domänensprache der Feuerwehr: `Mitglied`, `Einsatz`, `Pruefung`), technische Begriffe englisch (`Repository`, `DbContext`). Umlaute im Code ausgeschrieben (`ae`, `oe`, `ue`, `ss`), in der Oberfläche natürlich korrekt.

---

## 2. Überblick

```mermaid
erDiagram
    Mitglied ||--o{ Mitgliedschaft : "hat Zeiträume"
    Abteilung ||--o{ Mitgliedschaft : ""
    Mitglied ||--o{ DienstgradZuordnung : ""
    Dienstgrad ||--o{ DienstgradZuordnung : ""
    Mitglied ||--o{ FunktionZuordnung : ""
    Funktion ||--o{ FunktionZuordnung : ""
    Mitglied ||--o{ QualifikationNachweis : ""
    Qualifikation ||--o{ QualifikationNachweis : ""
    Mitglied ||--o| AtemschutzStatus : "berechnet"
    Mitglied ||--o{ AtemschutzVorsorge : ""
    Mitglied ||--o{ AtemschutzNachweis : ""
    Mitglied |o--o| Benutzer : "Login"
    Dienst ||--o{ Anwesenheit : ""
    Mitglied ||--o{ Anwesenheit : ""
    Einsatz ||--o{ EinsatzFahrzeug : ""
    Einsatz ||--o{ EinsatzTeilnahme : ""
    Mitglied ||--o{ EinsatzTeilnahme : ""
    Fahrzeug ||--o{ EinsatzFahrzeug : ""
    Einsatz ||--o| AlarmImport : "Quelle"
    Geraeteart ||--o{ Geraet : ""
    Geraeteart ||--o{ PruefplanPosition : ""
    Geraet ||--o{ Pruefung : ""
    Fahrzeug ||--o{ Geraet : "verlastet"
    Fahrzeug ||--o{ Pruefung : "HU/UVV"
    Geraet ||--o{ Mangel : ""
```

Module und ihre Ausbaustufe:

| Modul | Gerüst (jetzt) | Später |
|---|---|---|
| Mitglieder, Abteilungen, Dienstgrade, Funktionen | ✓ | Ehrungen, Führerscheine, Erziehungsberechtigte |
| Qualifikationen und Lehrgänge | ✓ (Katalog + Nachweis mit Ablauf) | Fortbildungskonto 40 h, Voraussetzungsketten |
| Atemschutz | ✓ (Vorsorge, Nachweis, berechneter Status) | Geräte-Lebenslauf Pressluftatmer |
| Dienst/Übung mit Anwesenheit | ✓ | Einladung, Zu-/Absage, iCal, QR-Anwesenheit |
| Einsatz mit Alarm-Import | ✓ | Kostenersatz § 34, Unfallmeldung UKBW, Freigabe-Workflow |
| Fahrzeuge, Geräte, Prüfungen | ✓ | Mängel-Workflow, QR-Etiketten, Ausleihe |
| Entschädigung § 16 | ✓ | `Abrechnung` mit eingefrorenen `AbrechnungsPosten`; Sätze als Parameter `Entschaedigung.{Vorgang}.{Feld}` und `Entschaedigung.Pauschale.{Funktionscode}` |
| Jugendfeuerwehr, Kleiderkammer, Kameradschaftskasse | – | eigene Module |

---

## 3. Gemeinsame Basis

Jede Entität erbt von `Entitaet`:

| Feld | Typ | Hinweis |
|---|---|---|
| `Id` | `Guid` | UUID v7, im Code erzeugt |
| `ErstelltAm`, `ErstelltVon` | `DateTimeOffset`, `Guid?` | vom Interceptor gesetzt |
| `GeaendertAm`, `GeaendertVon` | `DateTimeOffset?`, `Guid?` | |
| `GeloeschtAm` | `DateTimeOffset?` | globaler Query-Filter blendet aus |
| `Version` | `uint` (`xmin`) | optimistische Nebenläufigkeit über PostgreSQL-Systemspalte |

Kataloge erben von `Katalogeintrag`: `Code` (unique), `Bezeichnung`, `Kurzbezeichnung`, `Sortierung`, `Aktiv`.

`Parameter` (L4): `Schluessel` (z. B. `Altersgrenze.Einsatzdienst`, `Atemschutz.Vorsorge.IntervallMonate.Unter50`), `Wert` (text), `GueltigAb`, `GueltigBis`, `Quelle` (Freitext, z. B. "Satzung 2019 § 7").

---

## 4. Mitglieder

### Mitglied (Person)

| Feld | Typ | Pflicht | Hinweis |
|---|---|---|---|
| `Personalnummer` | `string(20)` | ja | unique; aus MP-FEUER übernommen |
| `LegacyMpFeuerId` | `string?` | | interne ID aus MP-FEUER für den Migrationsabgleich |
| `Barcode` | `string?` | | MP-FEUER-Barcode, später QR für Anwesenheit |
| `Anrede`, `Titel` | | | |
| `Vorname`, `Nachname` | `string(100)` | ja | |
| `Geburtsdatum` | `DateOnly` | ja | Altersgrenzen, Jugend, alamos `birthday` |
| `Geschlecht` | enum? `Maennlich`, `Weiblich`, `Divers` | | Jahresstatistik (Spalte „weiblich“), Dienstgradbezeichnung |
| `Strasse`, `Plz`, `Ort` | | | Wohnortwechsel ist anzeigepflichtig |
| `TelefonMobil` | `string?` | | **E.164** gespeichert (`+49…`), für alamos `0049…` formatiert |
| `TelefonFestnetz`, `Email` | | | privat |
| `Beruf`, `Arbeitgeber`, `ArbeitgeberStrasse`, `ArbeitgeberPlz`, `Arbeitsort`, `ArbeitgeberTelefon`, `ArbeitgeberMobil`, `ArbeitgeberEmail` | `string?` | | Tagesverfügbarkeit; nur mit Recht `Mitglieder.Arbeitgeber` (Kommandant, Stellvertreter, Führungskraft) sichtbar |
| `Notizen` | `string?` | | |
| `Fe2ExternalId` | `Guid` | ja | = `Id`, eigenes Feld, damit die alamos-`externalDbId` auch nach einer Neuanlage stabil bleibt |
| `Fe2SyncAktiv` | `bool` | | Person wird an alamos übertragen |

Bankdaten bewusst **nicht** im Gerüst (nur Kassier, eigenes Modul Entschädigung).

### Mitgliedschaft (historisiert)

| Feld | Typ | Hinweis |
|---|---|---|
| `MitgliedId`, `AbteilungId` | `Guid` | |
| `Status` | enum `Probezeit`, `Aktiv`, `Befreit`, `Ausgeschieden` | |
| `GueltigAb`, `GueltigBis` | `DateOnly`, `DateOnly?` | offener Zeitraum = aktuell |
| `Austrittsgrund` | enum? | `Altersgrenze`, `ProbezeitNichtBestanden`, `Gesundheit`, `Wohnortwechsel`, `Austritt`, `Ausschluss`, `Tod`, `Uebertritt` (Jugend → Einsatz, Einsatz → Alters) |
| `Vordienstzeit` | bool | Zeiten in anderen Wehren für Ehrenzeichen |
| `ProbezeitBis`, `ProbezeitBemerkung` | `DateOnly?`, `string?` | Ende der Probezeit, beim Eintritt vorbelegt; Verkürzung, Verlängerung oder Verzicht mit Beschluss (Satzung § 3 Abs. 2) |
| `Bemerkung` | | |

Regel: pro Mitglied höchstens eine offene Mitgliedschaft je Abteilung (Partial Unique Index `WHERE gueltig_bis IS NULL`). Probezeitende = `ProbezeitBis`, sonst `GueltigAb` + `Parameter Probezeit.Monate` (12). Probezeit gibt es nur in Einsatzabteilungen; bestanden ist sie erst mit der Bestätigung. Beim Übertritt bleiben alle Daten am Mitglied, nur Zug und Gruppe der alten Abteilung enden.

### Erziehungsberechtigter

Höchstens zwei je minderjährigem Mitglied (`Nummer` 1 oder 2): `Verhaeltnis` (Mutter, Vater, Vormund, sonstige), Name, Anschrift, `Telefon`, `Mobil`, `Email`. Gepflegt in den Stammdaten des Mitglieds, sichtbar in der Mitgliederakte mit demselben Datenbereich wie das Mitglied. Der Löschlauf löscht sie mit der Volljährigkeit (Parameter `Datenschutz.Erziehungsberechtigte.LoeschenAbAlter`, 18) oder zusammen mit den Kontaktdaten nach dem Austritt (Regel L15).

### Mitgliederakte (Oberfläche)

`/Mitglieder/Akte/{id}` fasst alles zu einem Mitglied zusammen: Person, Jugendfeuerwehr (Eintritt, Austritt mit Grund, Leistungsspange oder „keine Jugendfeuerwehr“), Abteilungen, Funktionen, Dienstgrade, Ehrungen, Qualifikationen, Dienstbeteiligung und offene Punkte (Beförderung, Ehrungen, Laufbahnhinweise, Fristen). Reiter führen zu Stammdaten, Laufbahn, Ausbildung und Fristen sowie Ausrüstung; jeder Teil erscheint nur mit dem passenden Recht.

### Abteilung (Katalog)

`Art` (`Einsatz`, `Jugend`, `Kinder`, `Alters`, `Musik`) bestimmt die Regeln, nicht der Code; so sind mehrere Einsatzabteilungen möglich (etwa je Ortsteil). `UebergeordneteId` hängt die Kinderfeuerwehr an die Jugendfeuerwehr. Seed: `EA` Einsatzabteilung, `JF` Jugendfeuerwehr, `KF` Kinderfeuerwehr (inaktiv, steht noch nicht in der Satzung; Alter über `Kinder.Aufnahme.AbAlter` 6 und `Kinder.Ende.Alter` 10), `AA` Altersabteilung.

Gepflegt unter Stammdaten → Abteilungen und Funktionen (Recht `Stammdaten.Organisation`: Administrator, Kommandant, Stellvertreter). Regeln: mindestens eine aktive Einsatzabteilung; die Kinderfeuerwehr braucht eine Jugendfeuerwehr als übergeordnete Abteilung; die `Art` bleibt fest, sobald es Mitgliedschaften gibt; eine Abteilung mit aktuellen Mitgliedern oder aktiven Unterabteilungen wird nicht abgeschaltet. Neue Abteilungen bekommen den Code aus der Art plus Nummer (`EA2`).

### Dienstgrad (Katalog) und DienstgradZuordnung

Katalog nach VwV Feuerwehrbekleidung (FM-Probe, FM, OFM, HFM, LM, OLM, HLM, BM, OBM, HBM, LHBM) mit `Sortierung` und `MindestwartezeitMonate` (12). Zuordnung: `MitgliedId`, `DienstgradId`, `Ab` (`DateOnly`), `Bemerkung`. Aktueller Dienstgrad = jüngste Zuordnung.

### Funktion (Katalog) und FunktionZuordnung

Funktionen nach FwG BW, Feuerwehrsatzung, Dienstvorschriften und UVV: Kommandant und Stellvertreter, Abteilungskommandant und Stellvertreter, Zug- und Gruppenführer, Gerätewart, Atemschutzgerätewart, Sicherheitsbeauftragter (§ 22 SGB VII, DGUV Vorschrift 1 § 20), Ersthelfer (DGUV Vorschrift 1 § 26), Ausbilder, Jugendfeuerwehrwart und Stellvertreter, Kinderfeuerwehrwart, Leiter der Altersabteilung, Kassier, Schriftführer, Kleiderwart, Kassenprüfer, Ausschussmitglied.

Katalogfelder: `Grundlage` (Rechtsgrundlage als Text), `Voraussetzungen` (Qualifikationscodes wie bei Lehrgängen, `|` oder, `;` und; Vorschläge nach VwV Feuerwehrausbildung und FwDV 2), `Abteilungsart` (die Funktion gehört zu einer Abteilung dieser Art), `AmtszeitJahre`, `VorgeschlageneRolle`.

| Feld | Hinweis |
|---|---|
| `MitgliedId`, `FunktionId` | |
| `AbteilungId` | bei Funktionen mit `Abteilungsart`, z. B. welche Einsatzabteilung jemand führt; gibt es nur eine passende, wird sie gesetzt |
| `Art` | `Wahl` oder `Bestellung` |
| `GueltigAb`, `GueltigBis` | bei Wahl automatisch `GueltigAb` + 5 Jahre als Vorschlag |
| `Beschlussdatum`, `Bemerkung` | |

**Funktion ist fachlich, Rolle ist technisch.** Eine Funktion kann eine Rolle vorschlagen (Feld `VorgeschlageneRolle` im Katalog), vergeben wird die Rolle bewusst vom Administrator. Grund: Ein Kassier braucht nicht automatisch Login-Rechte, und eine Vertretung braucht Rechte ohne Satzungsfunktion.

### Laufbahn: Übertritte, Ehrungen, Beförderungen (`Domain/Laufbahn`)

- **Übertritt, Probezeit, Ausscheiden** ändern nie eine Mitgliedschaft, sondern beenden sie am Vortag und legen eine neue an (`Mitgliedschaftswechsel`). Altersgrenzen aus Parametern: Aufnahme Einsatzabteilung ab 17, Einsatzdienst ab 18, Aufnahme Jugendfeuerwehr bis 17 (Mindestalter laut Ausschuss 10, Parameter `Jugend.Aufnahme.AbAlter`, 0 = keine Grenze), Ende Jugendfeuerwehr mit 18, Altersabteilung ab 55, Dienstende 65, Probezeit 12 Monate, Vorlauf der Hinweise 3 Monate.
- **Dienstzeit** (`Dienstzeitrechnung.Anrechnen`) nach VwV Feuerwehr-Ehrenzeichen i. d. F. vom 27.02.2025, Nr. 4:
  - Einsatzabteilung zählt; Vordienstzeiten (`Mitgliedschaft.Vordienstzeit` mit `Vordienstart`: andere Feuerwehr auch in anderen Bundesländern, Berufs- oder Werkfeuerwehr, Ausland) zählen.
  - Wehr-/Zivildienst und BFD/FSJ/FÖJ zählen nur, wenn vorher schon Feuerwehrdienst war.
  - Jugendfeuerwehr ab 14 (`Ehrung.Jugend.AnrechnenAbAlter`). Bei Eintritt ab `Ehrung.Jugend.LeistungsspangeAbEintritt` (01.01.2018) nur mit Leistungsspange (Qualifikation `LSP`); die VwV sagt „soll“, FEVAD behandelt es als Pflicht (Festlegung 22.10.2026).
  - Altersabteilung und Musikabteilung (Code `MU`) nur mit `Mitgliedschaft.EinsatzdienstBestaetigt` (Kommandant bestätigt Konzeption 65plus bzw. Grundausbildung und Übungsdienst); Altersabteilung bis 70 (`Ehrung.Altersabteilung.AnrechnenBisAlter`). Ohne Bestätigung zählt sie nicht.
  - Befreiungen (`Status` befreit mit `Befreiungsgrund`) zählen nicht, außer Krankheit und Mutterschutz; Kindererziehung bis `Ehrung.Kindererziehung.AnrechnenMonate` (12) je Befreiung, also je Kind eine Befreiung erfassen.
  - Doppelzeiten zählen einfach. Was nicht angerechnet wird, steht in `Anrechnung.NichtAngerechnet` und wird auf der Laufbahnseite rot angezeigt.
  - Zugehörigkeit (Vereinsjubiläum) = alle eigenen Abschnitte.
- **Ehrungsart** (Katalog): `Grundlage` (Einsatzdienst, Zugehörigkeit, Jahre in Funktion mit `FunktionCode`, Jahre in der Jugendfeuerwehr, nur von Hand), `Jahre`, `Reihe` (höhere Stufe ersetzt niedrigere), `VorstufeId` mit `MindestabstandJahre`, `NichtMitQualifikationen` (Codes, `text[]`), `Verleiher`, `Antragsweg`, `Kontingent` (Hinweis), `VorlaufWochen`, `MitAntragsschluss` (Verbandsehrungen, Parameter `Ehrung.Verband.Antragsschluss`, Vorschlag „30.11.“ für das Folgejahr).
  - Seed: Feuerwehr-Ehrenzeichen Bronze (15 Jahre), Silber (25), Gold (40), Gold-Sonderstufe (50) und für besondere Verdienste; Jubiläen 10 bis 60 Jahre; LFV-Ehrenmedaille Silber/Gold; DFV-Ehrenkreuz Bronze/Silber/Gold (Gold nach Silber); Deutsche Feuerwehr-Ehrenmedaille; Ehrenzeichen Bevölkerungsschutz; Traditionsnadel JF BW (2 Jahre JF ohne Jugendflamme/Leistungsspange); Ehrennadel JF BW Silber (5 Jahre Jugendfeuerwehrwart) und Gold (2 Jahre nach Silber); Ehrennadel DJF Silber/Gold (5 Jahre Abstand). KFV Göppingen und Gemeinde-Ehrungen sind pflegbar.
  - **Ehrung**: `Status` (vorgeschlagen, beantragt, verliehen, abgelehnt, verzichtet), `VoraussetzungAm`, `BeantragtAm`, `VerliehenAm`, `Urkundennummer`. Fehlt beim Erfassen die Vorstufe oder der Abstand, wird gespeichert und ein Hinweis gezeigt.
- **Befoerderungsweg** je Dienstgrad, einer genügt: `MindestjahreImDienstgrad`, `AlleQualifikationen`, `EineDerQualifikationen` (Codes, `text[]`), `ProbezeitBeendet`, `UeberspringenErlaubt` (LM mit GF, BM mit ZF), `NurVonHand`, `Hinweis`. Dazu `Dienstgrad.MindestwartezeitMonate` als Abstand zwischen zwei Beförderungen. Seed nach der Übersicht des Landratsamts Schwarzwald-Baar-Kreis zur VwV Feuerwehrbekleidung (Stand 11/2025).
- Erinnerungen laufen über `IFristQuelle` (`Laufbahnabfrage`) in die gemeinsame Fristenmail; Jugendfeuerwehr-Hinweise auch an den Jugendwart. Vorgeschlagene Verbandsehrungen erscheinen bis zum Antragsschluss.
- Probezeit und Altersgrenzen sind unter Stammdaten → Einstellungen der Abteilungen einstellbar (Recht `Laufbahn.Regeln`), jeweils als neuer Parameterwert ab heute.

### Personalprüfung und Pruefhinweis

`Personalpruefung` bewertet Mitglieder: mögliche Beförderung, fällige Ehrung, Laufbahnhinweise (Probezeit, Übertritt, Altersgrenze) und Funktionen (`Funktionsanlaesse`: fehlende Voraussetzung, Ende der Amtszeit im Vorlauf). Das Ergebnis steht in `Pruefhinweis`:

| Feld | Hinweis |
|---|---|
| `MitgliedId`, `Art` | Beförderung, Ehrung, Mitgliedschaft, Funktion |
| `Schluessel` | erkennt denselben Anlass wieder, eindeutig je Mitglied |
| `Text`, `FaelligAm`, `Pflicht` | |
| `Status` | offen, entfallen (Anlass weg, z. B. Lehrgang nachgetragen), zurückgestellt (von Hand; wird nicht erneut gemeldet) |
| `ErkanntAm`, `AbgeschlossenAm` | |

Ausgelöst wird sie vom `PruefAusloeser` (SaveChanges- und Transaktions-Interceptor): Änderungen an Mitglied, Mitgliedschaft, Dienstgrad, Funktion, Lehrgang oder Ehrung melden das Mitglied nach dem Commit an die `Pruefwarteschlange`; Änderungen an Regeln (Ehrungsarten, Beförderungswege, Dienstgrade, Funktionen, Qualifikationen, Parameter) melden alle. Der `PersonalpruefungDienst` arbeitet die Warteschlange ab, prüft beim Start alle (Parameter `Pruefung.GesamtlaufBeimStart`, 1) und danach täglich. Seite: Laufbahn → Prüfergebnisse.

---

## 5. Qualifikationen und Lehrgänge

### Qualifikation (Katalog)

| Feld | Hinweis |
|---|---|
| `Code`, `Bezeichnung` | z. B. `TM1`, `TM2`, `SPRF`, `AGT`, `MA`, `TF`, `GF`, `ZF`, `GW`, `CSA`, `FS-C` |
| `Art` | `Lehrgang`, `Fahrerlaubnis`, `Sonstiges`, `Abzeichen` |
| `GueltigkeitMonate` | `int?`, leer = unbefristet |
| `Fe2Funktion` | `string?`, Kürzel für alamos `osFunctions` (z. B. `AGT`, `MA`); nur **gültige** Qualifikationen werden übertragen |
| `Ebene` | `Standort`, `Kreis`, `Land` |
| `VorstufeId`, `VorstufeAbstandJahre` | Stufenfolge: Vorstufe nötig, frühestens n Kalenderjahre danach; die höhere Stufe löst die Vorstufe ab |
| `Voraussetzungen` | nötige Qualifikationen als Codes, z. B. `TM1; AGT\|SPRF` (Strichpunkt: alle, senkrechter Strich: eine davon). Gilt für Abzeichen und Lehrgänge; fehlende Voraussetzungen werden beim Eintragen gemeldet, sperren aber nicht (Nachträge). Seed nach FwDV 2: TM2, SPRF, AGT, MA, TH, MOTS, JGL ← TM1; TF ← TM2; GF ← TF; ZF ← GF; VF, LDF ← ZF; AUSB ← GF; AGW ← AGT; ABC1 ← TM2 und AGT; JFW ← JGL oder GF; AGT, MA, MOTS ab 18 |
| `MindestalterJahre`, `HoechstalterJahre`, `AlterNachJahrgang` | Altersgrenzen, bei der Jugendfeuerwehr nach Jahrgang |
| `MindestJugendMonate` | Mindestdauer in der Jugendfeuerwehr (Leistungsspange 12) |

### Abzeichen

Abzeichen werden nicht beantragt, sondern am Abnahmetag erworben und als `QualifikationNachweis` erfasst (Datum,
Abnahmeort in `Lehrgangsort`, `Urkunde`). Vorbelegt: Leistungsabzeichen BW Bronze/Silber/Gold (`LA-B`, `LA-S`, `LA-G`,
Richtlinie Neufassung 2026), Geschicklichkeitsprüfung Maschinisten (`GP-MA`), Kinderfunke 1/2 (`KF1`, `KF2`),
Jugendflamme I–III (`JFL1`–`JFL3`) und Leistungsspange DJF (`LSP`). Fehlende Voraussetzungen sperren das Eintragen
nicht (Nachträge), sie werden gemeldet. Die Planungshilfe Personal → Leistungsabzeichen prüft je Mitglied der
Einsatzabteilung die Stufe und die Funktionen am Abnahmetag: Maschinist mit gültiger Fahrerlaubnis, Atemschutz mit
gültiger Eignung und Belastungsübung höchstens `Leistungsabzeichen.Belastungsuebung.HoechstensMonate` (12) alt (nur
mit Atemschutz-Recht sichtbar), Gruppenführer mit Lehrgang.

Leistungsspange als Pflicht (Festlegung Andi, 22.10.2026): Für Mitglieder der Jugendfeuerwehr mit Eintritt ab
`Ehrung.Jugend.LeistungsspangeAbEintritt` (01.01.2018) entsteht ab `Ehrung.Jugend.AnrechnenAbAlter` (14) ohne Spange
eine Frist bis zum Übertritt (`Jugend.Ende.Alter`, 18), gelb und im letzten Zeitraum
(`Jugend.Leistungsspange.WarnungTage`, 365) rot; die Wochenmail geht an `Fristen.ErinnerungJugend`. Der Jugendwart trägt Jugendabzeichen unter Abteilungen → Jugendfeuerwehr → Jugendabzeichen ein (`Jugend.Bearbeiten`, nur aktuelle Mitglieder der Jugendfeuerwehr). Ohne Spange zählt die Jugendzeit nicht für das Ehrenzeichen.

### QualifikationNachweis

| Feld | Hinweis |
|---|---|
| `MitgliedId`, `QualifikationId` | |
| `ErworbenAm` | `DateOnly` |
| `GueltigBis` | `DateOnly?`, vorbelegt aus `GueltigkeitMonate`, überschreibbar |
| `Lehrgangsort`, `Stunden`, `Bemerkung` | bei Abzeichen ist `Lehrgangsort` der Abnahmeort |
| `Urkunde` | Nummer oder Ablageort von Urkunde bzw. Besitzzeugnis |
| `DokumentId` | `Guid?`, Zeugnis-Upload (Dateiablage folgt) |

Berechnet (nicht gespeichert): Status `Gueltig`, `LaeuftAb` (Parameter `Frist.Warnung.Tage`, Default 90), `Abgelaufen`.

### Nachweisart (Katalog) und Nachweis

Wiederkehrende Nachweise und Untersuchungen, z. B. Führerscheinkontrolle (`FSK`, 6 Monate) und jährliche
Unterweisung (`UVV`, 12 Monate). Weitere Arten pflegt der Kommandant unter Personal → Kataloge. Eine Art mit
`Gesundheitsbezogen` (z. B. ärztliche Untersuchung) ist nur mit Atemschutz-Recht sichtbar und eintragbar; gespeichert
werden auch dann nur Datum und Frist.

| Feld | Hinweis |
|---|---|
| `Nachweisart.IntervallMonate` | Katalogwert; daraus wird `Nachweis.GueltigBis` vorbelegt |
| `Nachweisart.Pflicht` | `Keine`, `AlleEinsatzkraefte`, `MitFahrerlaubnis` (Qualifikation der Art Fahrerlaubnis), `MitQualifikation` (+ `QualifikationId`) |
| `Nachweis` | `MitgliedId`, `NachweisartId`, `Datum`, `GueltigBis`, `DurchgefuehrtVon`, `Bemerkung` |

### Fristen (berechnet)

`Personalfristen` berechnet je aktivem Mitglied der Einsatzabteilung die Fristen aus befristeten
Qualifikationen, Pflicht-Nachweisen und – bei Inhabern der Qualifikation `AGT` – Vorsorge und den drei
jährlichen Atemschutzpflichten. Ein fehlender Pflicht-Nachweis ist rot ohne Datum. Warnfristen:
`Frist.Warnung.Tage` (Qualifikationen), `Ausbildung.Nachweis.WarnungTage`, `Atemschutz.WarnungTage`.
Intervalle je Atemschutzpflicht: `Atemschutz.Belastungsuebung|Einsatzuebung|Unterweisung.IntervallMonate` (12).
Vorschlag für die nächste Vorsorge: `Atemschutz.Vorsorge.IntervallMonate` (36), ab
`Atemschutz.Vorsorge.KuerzeresIntervallAbAlter` (50) `Atemschutz.Vorsorge.IntervallAbAlterMonate` (12). Alle Werte
ändert der Kommandant unter Personal → Einstellungen; eine Änderung legt einen neuen Parameterwert ab heute an.

Die Wochenmail (`FristErinnerung`) sammelt alle `IFristQuelle`-Implementierungen und schickt jedem Konto die
Fristen, deren Empfänger-Policy es über seine Rollen erfüllt (`Fristen.ErinnerungPersonal`,
`Fristen.ErinnerungAtemschutz`). Intervall und frühste Uhrzeit: `Frist.Erinnerung.IntervallTage`,
`Frist.Erinnerung.AbStunde`; jeder Lauf steht in `Erinnerungslauf`.

---

## 6. Atemschutz (eigene Policy `Atemschutz.Lesen`)

| Entität | Felder | Hinweis |
|---|---|---|
| `AtemschutzVorsorge` | `MitgliedId`, `UntersuchtAm`, `Ergebnis` (`Geeignet`, `GeeignetBefristet`, `NichtGeeignet`), `NaechsteFaellig`, `Bemerkung` (max. 200 Zeichen, **kein Befund**) | Art. 9 DSGVO; nur Kommandant, Stellvertreter, Atemschutzgerätewart |
| `AtemschutzNachweis` | `MitgliedId`, `Datum`, `Art` (`Einsatz`, `Belastungsuebung`, `Einsatzuebung`, `Unterweisung`), `Geraetetyp`, `Minuten`, `Taetigkeit`, `Ort`, `EinsatzId?`, `DienstId?` | aus Einsatz/Dienst automatisch erzeugbar |
| `AtemschutzStatus` (View/Berechnung) | `Tauglich`, `LehrgangVorhanden`, `UebungenAktuell`, `Ampel` | **Nicht gespeichert**, sondern berechnet aus Vorsorge + Nachweisen + Qualifikation `AGT`. Sperre, wenn eine der drei Übungsarten älter als 12 Monate. |

---

## 7. Dienstbetrieb: Dienst/Übung mit Anwesenheit

### Dienst

| Feld | Typ | Hinweis |
|---|---|---|
| `Art` | enum | `Uebung`, `Unterricht`, `Sonderdienst`, `Arbeitsdienst`, `Sitzung`, `Hauptversammlung`, `Veranstaltung`, `Lehrgang` (extern) |
| `Titel`, `Thema` | | |
| `Beginn`, `Ende` | `DateTimeOffset` | Default Do 19:30 für EA, Di 19:00 für JF |
| `Ort` | | Default Gerätehaus Kirchstraße 5 |
| `AbteilungId` | `Guid?` | Zielgruppe; leer = alle |
| `GruppeId` | `Guid?` | Zielgruppe zusätzlich auf eine Gruppe eingeschränkt |
| `QualifikationId` | `Guid?` | Zielgruppe zusätzlich auf Träger einer am Diensttag gültigen Qualifikation (AGT, Maschinist) |
| `SerieId` | `Guid?` | Teil einer `DienstSerie` |
| `RueckmeldungErbeten` | bool | Mitglieder der Zielgruppe sollen zu- oder absagen |
| `VerantwortlicherId` | `Guid?` | Mitglied (Übungsleiter) |
| `Status` | enum | `Geplant`, `Durchgefuehrt`, `Abgesagt` |
| `ZaehltFuerFortbildung` | bool | 40-h-Standortfortbildung |
| `Bericht` | text | Freitext, Markdown |

### Anwesenheit

| Feld | Hinweis |
|---|---|
| `DienstId`, `MitgliedId` | unique zusammen |
| `Status` | `Anwesend`, `Entschuldigt`, `Unentschuldigt` |
| `Von`, `Bis` | `DateTimeOffset?`, leer = ganze Dienstdauer |
| `Funktion` | Freitext/Katalog (Ausbilder, Teilnehmer) |
| `ErfasstUeber` | `Web`, `Qr`, `Notbetrieb`, `Rueckmeldung` (aus Zu-/Absage übernommen) |
| `Bemerkung` | |

Stunden je Person werden aus `Von/Bis` oder der Dienstdauer **berechnet**, nicht gespeichert.

### DienstSerie

`Rhythmus` (`Woechentlich`, `Zweiwoechentlich`, `Vierwoechentlich`, `MonatlichWochentag`), `ErsterTag`, `LetzterTag`.
Die Termine werden beim Anlegen als eigenständige Dienste erzeugt (höchstens `Dienst.Serie.MaxMonate` im Voraus);
ein einzelner Termin lässt sich danach verschieben oder absagen. Änderungen können auf die folgenden Termine übertragen werden.

### Rueckmeldung (Zu-/Absage)

| Feld | Hinweis |
|---|---|
| `DienstId`, `MitgliedId` | unique zusammen; eine neue Rückmeldung ersetzt die alte |
| `Antwort` | `Zusage`, `Absage` |
| `Begruendung` | freiwillig, nur bei Absage, nur für Führungskräfte sichtbar |
| `GemeldetAm` | `DateTimeOffset` |

Möglich bis `Dienst.Rueckmeldung.SchlussStunden` vor Beginn. Löschfristen (Vorschlag, DSB-Freigabe offen, angewendet vom Löschlauf):
Begründung nach `Dienst.Rueckmeldung.BegruendungLoeschenMonate` (3), Rückmeldung nach `Dienst.Rueckmeldung.LoeschenMonate` (12) Monaten nach dem Dienst. Führungskräfte übernehmen Zusagen als „anwesend“
und Absagen als „entschuldigt“ in die Anwesenheit, wo noch nichts erfasst ist.

### Gruppe (Katalog) und GruppenMitgliedschaft

Untergliederung der Abteilungen: `Art` `Zug` oder `Gruppe`, `AbteilungId` (leer = abteilungsübergreifend, z. B. Führungsgruppe), `UebergeordneteId` (Gruppe im Zug). Auch Zielgruppe für Dienste.

### Kalender-Abo

`Benutzer.KalenderToken` ist der geheime Teil des iCal-Links `/kalender/{token}.ics`. Der Feed enthält nur die Termine
der eigenen Zielgruppe (`Dienst.Kalender.RueckblickMonate` zurück, `Dienst.Kalender.VorschauMonate` voraus) und die
eigene Rückmeldung, keine Namen anderer Mitglieder. Neu erzeugen macht den alten Link ungültig.

---

## 8. Einsatz mit Import aus Leitstelle/alamos

Der Einsatzbericht ist die Drehscheibe: daraus entstehen später Entschädigung, Atemschutznachweise, Prüfungen nach Gebrauch, Kostenersatz und Jahresstatistik. Der Ablauf: **Alarmdaten kommen als Entwurf, der Einsatzleiter ergänzt, der Kommandant gibt frei.**

### AlarmImport (Rohdaten, getrennt vom Bericht)

Warum getrennt: Die Rohdaten unterliegen der 6-Monats-Grenze für Alarmdaten (IM BW, § 35 Abs. 6 FwG) und werden danach gelöscht. Der Einsatzbericht selbst bleibt länger (Dokumentationspflicht, Kostenersatz bis 10 Jahre).

| Feld | Typ | Quelle |
|---|---|---|
| `Quelle` | enum `Fe2Webhook`, `Fe2Feedback`, `Manuell`, `MpFeuerMigration` | |
| `ExterneId` | `string` | alamos `externalId` bzw. Leitstellen-Einsatznummer; unique je Quelle (Idempotenz) |
| `EmpfangenAm` | `DateTimeOffset` | |
| `Rohdaten` | `jsonb` | komplette Payload, da das FE2-HTTP-Ausgangsformat erst an der echten Instanz getestet wird |
| `Verarbeitet` | bool | |
| `EinsatzId` | `Guid?` | erzeugter oder zugeordneter Einsatz |
| `LoeschenAb` | `DateOnly` | `EmpfangenAm` + 6 Monate |

Rückmeldungen (`GET /rest/addressbook/external/{alarmId}/feedback`) landen ebenfalls als `AlarmImport` mit Quelle `Fe2Feedback` und befüllen Vorschläge für `EinsatzTeilnahme` (Status `YES`/`HERE` → Vorschlag "teilgenommen").

### Einsatz

| Feld | Typ | Hinweis / Import-Mapping |
|---|---|---|
| `Einsatznummer` | `string(20)` | Format aus Parameter `Einsatz.Nummer.Format` (Standard `{JJJJ}-{NNN}`), laufend je Kalenderjahr der Alarmierung, unique; offline `N-…` |
| `LeitstellenNummer` | `string?` | ← `externalId` (FE2), MP-FEUER `Leitstellen_Nr` |
| `Stichwort` | `string?` | ← `keyword`, z. B. "B 2", "TH 1" |
| `StichwortBeschreibung` | `string?` | ← `keyword_description` |
| `StichwortKategorie` | `string?` | ← `keyword_category` |
| `Meldung` | text | ← `keyword_misc` + `message[]` (zeilenweise) |
| `Strasse`, `Hausnummer`, `Plz`, `Ort`, `Ortsteil` | | ← `location.street/house/postalCode/city/district` |
| `Objekt` | `string?` | ← `building` |
| `Latitude`, `Longitude` | `double?` | ← `location.coordinate` |
| `Alarmiert` | `DateTimeOffset` | Pflicht; ← `timestamp` |
| `Ausgerueckt` | `DateTimeOffset?` | erstes Fahrzeug |
| `EintreffenEinsatzort` | `DateTimeOffset?` | |
| `Einsatzende` | `DateTimeOffset?` | |
| `EinsatzbereitschaftHergestellt` | `DateTimeOffset?` | Entschädigung und Kostenersatz rechnen bis hier |
| `StatistikKategorieId` | `Guid` | **Pflicht bei Freigabe**: Brand, Technische Hilfe, Rettungsdienstlich, Sonstige, Fehlalarm (Landesstatistik) |
| `RechtsgrundlageArt` | enum | `Para2Abs1` (Pflichtaufgabe, grundsätzlich unentgeltlich), `Para2Abs2` (immer kostenpflichtig) |
| `Einsatzleiter` | `Guid?` | Mitglied |
| `Lagebericht`, `Massnahmen` | text | |
| `Gerettete`, `Verletzte`, `Tote`, `VerletzteFeuerwehr` | `int` | Landesstatistik |
| `Ueberoertlich` | bool | Überlandhilfe |
| `Status` | enum | `Entwurf`, `InBearbeitung`, `Freigegeben` |
| `FreigegebenAm`, `FreigegebenVon` | | nach Freigabe nur noch Kommandant änderbar, jede Änderung im Audit |
| `ErfasstImNotbetrieb` | bool | |

Kostenersatz (Tatbestand, Kostenpflichtiger, Sonderlöschmittel) und Unfallmeldung kommen als eigene Tabellen in einem späteren Schritt, damit die Personenbezüge Betroffener getrennt und gezielt löschbar bleiben.

### EinsatzFahrzeug

`EinsatzId`, `FahrzeugId`, `Ausgerueckt`, `Eingetroffen`, `Zurueck`, `Einsatzbereit` (alles `DateTimeOffset?`), `Kilometer` (`int?`). Verknüpfung zu FE2-`vehicles[]` über den **exakten Funkrufnamen**.

### EinsatzTeilnahme

`EinsatzId`, `MitgliedId`, `EinsatzFahrzeugId?`, `Funktion` (Maschinist, Gruppenführer, Angriffstrupp, …), `Von`, `Bis`, `Atemschutz` (bool, Minuten → erzeugt `AtemschutzNachweis`), `Quelle` (`Manuell`, `Fe2Rueckmeldung`), `Bestaetigt` (Self-Service-Bestätigung durch das Mitglied).

---

## 9. Fahrzeuge, Geräte, Prüfungen

### Fahrzeug

| Feld | Hinweis |
|---|---|
| `Funkrufname` | unique, z. B. "Florian Adelberg 43"; **muss exakt der FE2-Einsatzmittelkennung entsprechen** |
| `Typ` | HLF 10, TLF 8/18, MTW, Anhänger |
| `Kennzeichen`, `Hersteller`, `Aufbau`, `Baujahr`, `IndienstAm`, `AusserDienstAm` | |
| `Issi`, `FmsKennung` | TETRA-Kennungen |
| `Sitzplaetze` | |
| `PruefvorlageId?` | Geräteart mit `FuerFahrzeuge`, liefert den Prüfplan (HU, UVV) |

Seed für Entwicklung: HLF 10 (43, 2023), TLF 8/18 (21, 1986), MTW (19, 1999), Schlauchanhänger (1999).

### Geraeteart (Katalog) und PruefplanPosition

`Geraeteart`: `Bezeichnung` (z. B. "Hydraulisches Rettungsgerät", "Steckleiter", "Haltegurt"), `AussonderungNachJahren?` (Lebensdauer ab Herstellung), `Pruefgrundlage` (Text, z. B. "DGUV 305-002 Tab. 1"), `FuerFahrzeuge` (Prüfvorlage für Fahrzeuge). Der Seed liefert 17 Gerätearten nach DGUV 305-002 Tab. 1 sowie Vorschläge für Elektro, Fahrzeuge und Sonstiges.

`PruefplanPosition` (n je Geräteart): `Pruefart` (`Sichtpruefung`, `Funktionspruefung`, `Belastungspruefung`, `NachGebrauch`, `Hersteller`, `Hauptuntersuchung`, `Uvv`, `Elektro`), `IntervallMonate` (0 = keine feste Frist), `Pruefer` (Text), `Checkliste` (`jsonb`: Liste von `Pruefpunkt` mit Typ Ja/Nein, Messwert mit Einheit, Text). JSONB ist hier bewusst gewählt: Checklisten ändern sich, und jedes Protokoll speichert den Stand, mit dem geprüft wurde.

Fälligkeit (`Pruefplanung`, berechnet, nie gespeichert): je Position zählt die jüngste **bestandene** Prüfung derselben Prüfart; ohne Prüfung ist die Position sofort fällig (rot). Warnfrist aus Parameter `Technik.Pruefung.WarnungTage`, Aussonderung mit `Frist.Warnung.Tage`.

### Geraet

`Inventarnummer` (unique), `GeraeteartId`, `Bezeichnung`, `Hersteller`, `Typ`, `Seriennummer`, `Baujahr`, `Herstelldatum`, `Groesse` (PSA), `BeschafftAm`, `Anschaffungspreis`, `IndienstAm`, `AusgesondertAm`, `Aussonderungsgrund`, `FahrzeugId?` (verlastet auf), `Lagerort` (Fach), `BeschaffungId?`. Der QR-Code am Gerät enthält `{OeffentlicheAdresse}/g/{Inventarnummer}`: Technik-Berechtigte landen auf der Geräteseite, alle anderen bei „Mangel melden“. Der Zustand (einsatzbereit, eingeschränkt, außer Betrieb, ausgesondert) wird aus offenen Mängeln berechnet.

### Pruefung (Prüfprotokoll)

| Feld | Hinweis |
|---|---|
| `GeraetId?` oder `FahrzeugId?` | genau eins gesetzt (Check-Constraint) |
| `PruefplanPositionId?` | |
| `Pruefart`, `Datum` | |
| `Ergebnis` | `Bestanden`, `BestandenMitMaengeln`, `NichtBestanden`, `Ausgesondert` |
| `Ergebnisse` | `jsonb`: ausgefüllte Checkliste mit Messwerten |
| `NaechsteFaellig` | vorbelegt aus Intervall |
| `PrueferId` | Mitglied; muss Qualifikation `GW` oder Bestellung "befähigte Person" haben |
| `PrueferExtern` | Firma bei Herstellerprüfung |
| `EinsatzId?` | Prüfung nach Gebrauch |
| `Abgeschlossen` | nach Abschluss **unveränderlich** (Korrektur nur als neue Prüfung mit Verweis); PDF über QuestPDF |
| `ErfasstImNotbetrieb` | |

Aufbewahrung: über die gesamte Nutzungsdauer des Geräts (DGUV 305-002).

### Mangel

`GeraetId?`/`FahrzeugId?`, `GemeldetVon`, `GemeldetAm`, `Beschreibung`, `Schwere` (`Info`, `Einschraenkung`, `AusserBetrieb`), `Status` (`Offen`, `InArbeit`, `Behoben`), `BehobenAm`, `Massnahme`, `PruefungId?`. Melden darf jedes Mitglied (`Technik.MaengelMelden`), bearbeiten der Gerätewart. Entsteht auch aus einer Prüfung mit Mängeln und aus „beschädigt“ bei der Inventur.

### Inventur

`Inventur`: `Bezeichnung`, `Stichtag`, `FahrzeugId?` (leer = ganzer Bestand), `AbgeschlossenAm`. `InventurPosition` je Gerät mit `SollStandort` (Stand beim Anlegen), `Befund` (`Vorhanden`, `Beschaedigt`, `Fehlt`, leer = nicht erfasst), `ErfasstAm/Von`, `Bemerkung`. Nach dem Abschluss unveränderlich.

### Beschaffung

`Bezeichnung`, `Menge`, `Begruendung`, `Prioritaet`, `Status` (`Bedarf` → `Angemeldet` → `Genehmigt` → `Bestellt` → `Geliefert`, jederzeit `Abgelehnt`), `GeraeteartId?`, `FahrzeugId?`, `MangelId?` (Ersatzbeschaffung), `Haushaltsjahr`, `Kostenschaetzung` (gesamt), `Foerderfaehig`, `GenehmigtAm/Von`, `Lieferant`, `Bestellnummer`, `BestelltAm`, `GeliefertAm`, `Istkosten`. Genehmigen nur Kommandant und Stellvertreter (`Beschaffung.Freigeben`); beim Eingang entstehen Geräte mit Verweis auf die Beschaffung.

---

## 10. Benutzer, Rollen und Rechte

`Benutzer` ist der Identity-User (`IdentityUser<Guid>`), mit optionalem Verweis `MitgliedId` (1:1, eindeutig, beim Löschen des Mitglieds `NULL`). Getrennt, weil nicht jedes Mitglied einen Login hat (Jugend, Altersabteilung) und ein Gemeinde-Lesezugang kein Mitglied ist. Dazu `LetzteAnmeldungAm`, `DeaktiviertAm` und `AnonymisiertAm`: Konten werden zuerst deaktiviert; nach `Datenschutz.Konto.LoeschenNachDeaktivierungMonate` leert der Löschlauf Name, E-Mail, Passwort, Telefon, Kalender-Token und Rollen, die ID bleibt für Protokoll und Lesebestätigungen. Änderungen an Konten und Rollenvergaben schreibt der Audit-Interceptor mit, Passwort-Hash und Sicherheitsstempel nur als „geändert“.

Rollen (technisch, in Identity):

| Rolle | Kernrechte | 2FA Pflicht |
|---|---|---|
| `Administrator` | Benutzer, Rollen, Kataloge, Parameter | ja |
| `Kommandant` | alles fachlich, Freigaben, Atemschutz | ja |
| `Stellvertreter` | wie Kommandant | ja |
| `Fuehrungskraft` | Dienste und Anwesenheit schreiben, Einsatzentwürfe, Mitglieder lesen (ohne Gesundheit, Bank) | empfohlen (nicht erzwungen) |
| `Geraetewart` | Fahrzeuge, Geräte, Prüfungen, Mängel | ja |
| `Atemschutzgeraetewart` | Atemschutz (Vorsorge, Nachweise), Atemschutzgeräte | ja |
| `Jugendwart` | Mitglieder und Dienste **nur der Jugendfeuerwehr** | ja |
| `Kassier` | Entschädigung, Kameradschaftskasse (spätere Module) | ja |
| `Schriftfuehrer` | Dienste/Sitzungen, Protokolle, Mitgliederliste lesen | ja |
| `Mitglied` | eigene Daten, eigene Fristen und Stunden, Dienstplan, Mangel melden | nein |
| `GemeindeLesen` | Berichte und Statistik lesen (optional) | ja |

Die Tabelle zeigt die **Vorbelegung**. Was eine Rolle darf, pflegt der Administrator unter Verwaltung → Rollen und Rechte; dort lassen sich auch eigene Rollen anlegen (z. B. „Leitung Altersabteilung“), die immer die Zwei-Faktor-Anmeldung verlangen.

Umsetzung: Seiten prüfen **Policies**, nie Rollen. Jedes Recht steht mit Bereich, Art (lesen, anlegen, bearbeiten, löschen, Sonderrecht, Erinnerung) und Vorbelegung in `Berechtigungen.Katalog` (`Infrastructure/Identitaet/Berechtigungen.cs`). Gespeichert wird in:

| Tabelle | Inhalt |
|---|---|
| `rollenprofile` | `Name` (= Identity-Rolle), `Bezeichnung`, `Beschreibung`; mitgelieferte Rollen sind nicht löschbar |
| `rollen_rechte` | `Rolle`, `Recht`, `Abteilungen` (`uuid[]`, leer = alle Abteilungen; eine Abteilung schließt ihre Unterabteilungen ein) |
| `rechte_eingefuehrt` | Rechte, deren Vorbelegung schon übernommen ist; ein neues Recht im Code wird beim Start genau einmal vorbelegt |

`Rechteverzeichnis` hält die Rechte im Speicher (nach Änderungen sofort, sonst alle fünf Minuten neu geladen); `RechtHandler` prüft eine Policy gegen die Rollen-Claims. Eine Policy ist erfüllt, wenn eine Rolle das Recht für **alle** Abteilungen hat. Rechte mit Abteilungsbezug (`Mitglieder.Lesen`, `Mitglieder.Anlegen`, `Mitglieder.Bearbeiten`, `Dienste.AnwesenheitLesen`, `Dienste.Bearbeiten`) können auf Abteilungen beschränkt sein; das wertet der `Datenbereich` aus (Vorbelegung: Jugendwart für die Jugendfeuerwehr samt Kinderfeuerwehr). Abgeleitete Policies: `Mitglieder.Sehen` und `Dienste.AnwesenheitSehen` (Recht für irgendeine Abteilung), `Jugend.Lesen` und `Jugend.Bearbeiten` (Recht für eine Jugend- oder Kinderfeuerwehr).

Schutzschranken: Rechte auf Gesundheitsdaten (Atemschutz, Befreiungsgrund) kommen nur mit ausdrücklicher Bestätigung dazu; der Administrator behält `Verwaltung` und `Benutzer.Verwalten`; eine Rolle mit Konten lässt sich nicht löschen. Jede Änderung schreibt der Audit-Interceptor ins Änderungsprotokoll.

---

### Datenschutz: Löschlauf, Löschnachweis, Selbstauskunft

Der Hintergrunddienst `LoeschlaufDienst` startet `Loeschlauf` einmal täglich ab `Datenschutz.Loeschlauf.AbStunde` (Ortszeit), nicht im Notbetrieb; Administrator, Kommandant und Stellvertreter stoßen ihn unter **Datenschutz** auch von Hand an (`Datenschutz.Loeschlauf`). Regeln nach Löschkonzept, alle Fristen als Parameter:

| Regel | Was | Parameter |
|---|---|---|
| L1 | `AlarmImport` ab `LoeschenAb` löschen | `Einsatz.Alarmdaten.AufbewahrungMonate` (beim Empfang) |
| L6, L14 | Ausgetretene (keine laufende Mitgliedschaft mehr): Anschrift, Telefon, E-Mail, Arbeitgeber, Arbeitsort, Notizen, Barcode leeren, FE2-Abgleich aus, Größen löschen; der Restdatensatz bleibt (L7) | `Datenschutz.Kontaktdaten.LoeschenNachAustrittMonate` |
| L10a | `Erinnerungslauf` löschen | `Datenschutz.Erinnerungslauf.AufbewahrungMonate` |
| L18 | deaktivierte Konten anonymisieren (`AnonymisiertAm`) | `Datenschutz.Konto.LoeschenNachDeaktivierungMonate` |
| L19 | `AuditEintrag` löschen | `Datenschutz.Aenderungsprotokoll.AufbewahrungMonate` |
| L24, L25 | Absage-Begründung leeren, Rückmeldung löschen | `Dienst.Rueckmeldung.*` |

Gelöscht wird mit `ExecuteDelete`/`ExecuteUpdate` am Audit-Interceptor vorbei; die Protokolleinträge der betroffenen Datensätze werden geschwärzt (`Aenderungen` = `{"geschwaerzt": true}`, wer und wann bleibt). Je Regel mit Treffern und je Lauf entsteht ein `Loeschnachweis` (`Zeitpunkt`, `Regel`, `Anzahl`, `AusgeloestVon?`), ohne Inhalte. Die Begründung einer Absage protokolliert der Interceptor nur noch als „geändert“.

Dokumente löschen Kommandant und Stellvertreter einzeln endgültig (`Dokumente.EndgueltigLoeschen`): ganz, oder nur die älteren Versionen; mit Dateien, Lesebestätigungen und Schwärzung, Verweise aus Sitzungen werden geleert.

`/Konto/MeineDaten` zeigt jedem Konto die eigenen Daten (Art. 15) und exportiert sie als JSON. Die Abschnitte entstehen aus dem Modell: das Mitglied selbst und jede Tabelle mit Fremdschlüssel auf `Mitglied` oder `Benutzer`; neue Module erscheinen ohne Zutun.

## 11. Notbetrieb (Stufe 2, im Modell schon berücksichtigt)

| Element | Gerüst | Stufe 2 |
|---|---|---|
| UUID-Schlüssel, vorläufige Einsatznummer | ✓ | |
| Feld `ErfasstImNotbetrieb` an Anwesenheit, Einsatz, Pruefung | ✓ | |
| Konfigurationsschalter `Betrieb:Notbetrieb` (bool) | ✓ (nur gelber Balken) | Schreibzugriffe umlenken |
| Tabelle `OutboxEintrag` (`Typ`, `Payload jsonb`, `ErstelltAm`, `UebertragenAm`, `Fehler`) | – | lokal auf dem Gerätehaus-PC |
| Logische Replikation PostgreSQL Cloud → Gerätehaus | – | ✓ |

---

## 12. Projektgerüst (Solution-Struktur)

```
ff-adelberg-verwaltung/
├─ src/
│  ├─ Fevad.Domain/          Entitäten, Enums, Regeln (keine Abhängigkeiten)
│  ├─ Fevad.Infrastructure/  EF Core DbContext, Konfigurationen, Migrationen, Interceptoren, Seed
│  └─ Fevad.Web/             Razor Pages, HTMX-Partials, Identity, Policies, Minimal-API (/api/alarm/fe2)
├─ tests/
│  ├─ Fevad.Domain.Tests/    xUnit, reine Regeltests (Fristen, Status)
│  └─ Fevad.Integration.Tests/  xUnit + Testcontainers (PostgreSQL), WebApplicationFactory
├─ docker-compose.yml             postgres, mailpit, app (Profil "app")
├─ Dockerfile                     Multi-Stage, aspnet:10.0
├─ .github/workflows/ci.yml       build, test, Image nach GHCR
├─ Directory.Build.props, Directory.Packages.props (zentrale Paketversionen)
├─ global.json                    .NET 10 SDK
├─ LICENSE                        EUPL-1.2
└─ README.md                      Einrichtung unter Windows mit VS Code
```

Paketumfang des Gerüsts: `Npgsql.EntityFrameworkCore.PostgreSQL`, `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Design`, `EFCore.NamingConventions` (snake_case-Tabellen), xUnit, `Testcontainers.PostgreSql`. QuestPDF, ClosedXML und Hangfire kommen mit den ersten Funktionen, die sie brauchen (Prüfprotokoll-PDF, Export, Fristenjob), damit das Gerüst klein und verständlich bleibt.

HTMX und Alpine.js werden als Dateien in `wwwroot/lib` mitgeliefert (kein CDN, funktioniert auch im Gerätehaus ohne Internet). Tailwind über das Standalone-Binary, ohne Node.

---

## 13. Offene Fragen an Andi

1. **Personalnummer:** Gibt es eine Personalnummer der Gemeinde (für den Kämmerei-Export) neben der MP-FEUER-Personalnummer? Dann zwei Felder.
2. **Abteilungen:** Gibt es in Adelberg Gruppen/Züge innerhalb der Einsatzabteilung, die für Dienste oder Rechte relevant sind?
3. **Einsatznummer:** Format `JJJJ-NNN` in Ordnung, oder führt ihr heute ein anderes Schema?
4. **Einsatzfreigabe:** Reicht Kommandant/Stellvertreter, oder soll auch der Einsatzleiter selbst freigeben dürfen?
5. **Rollen:** Ist die Liste in Abschnitt 10 vollständig? Fehlt z. B. ein Leiter Atemschutz getrennt vom Atemschutzgerätewart oder ein Funk-Gerätewart?
