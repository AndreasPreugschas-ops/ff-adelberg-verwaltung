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
| L7 | **Soft-Delete und Löschfristen** | `GeloeschtAm` statt physischem Löschen; ein Hangfire-Job löscht oder anonymisiert nach Löschkonzept (z. B. Alarmrohdaten nach 6 Monaten, Kostenersatz nach 10 Jahren). |
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
| `Strasse`, `Plz`, `Ort` | | | Wohnortwechsel ist anzeigepflichtig |
| `TelefonMobil` | `string?` | | **E.164** gespeichert (`+49…`), für alamos `0049…` formatiert |
| `TelefonFestnetz`, `Email` | | | |
| `Arbeitgeber`, `Arbeitsort` | `string?` | | Tagesverfügbarkeit (nur Führung sichtbar) |
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
| `Bemerkung` | | |

Regel: pro Mitglied höchstens eine offene Mitgliedschaft je Abteilung (Partial Unique Index `WHERE gueltig_bis IS NULL`). Probezeitende = `GueltigAb` + `Parameter Probezeit.Monate` (12).

### Abteilung (Katalog)

Seed: `EA` Einsatzabteilung, `JF` Jugendfeuerwehr, `AA` Altersabteilung. Kinderfeuerwehr gibt es in Adelberg nicht, der Katalog lässt sie aber zu.

### Dienstgrad (Katalog) und DienstgradZuordnung

Katalog nach VwV Feuerwehrbekleidung (FM-Probe, FM, OFM, HFM, LM, OLM, HLM, BM, OBM, HBM, LHBM) mit `Sortierung` und `MindestwartezeitMonate` (12). Zuordnung: `MitgliedId`, `DienstgradId`, `Ab` (`DateOnly`), `Bemerkung`. Aktueller Dienstgrad = jüngste Zuordnung.

### Funktion (Katalog) und FunktionZuordnung

Satzungsfunktionen: Kommandant, Stellvertreter, Abteilungskommandant, Gruppenführer, Gerätewart, Atemschutzgerätewart, Jugendwart, Kassier, Schriftführer, Kleiderwart, Kassenprüfer, Ausschussmitglied.

| Feld | Hinweis |
|---|---|
| `MitgliedId`, `FunktionId` | |
| `Art` | `Wahl` oder `Bestellung` |
| `GueltigAb`, `GueltigBis` | bei Wahl automatisch `GueltigAb` + 5 Jahre als Vorschlag |
| `Beschlussdatum`, `Bemerkung` | |

**Funktion ist fachlich, Rolle ist technisch.** Eine Funktion kann eine Rolle vorschlagen (Feld `VorgeschlageneRolle` im Katalog), vergeben wird die Rolle bewusst vom Administrator. Grund: Ein Kassier braucht nicht automatisch Login-Rechte, und eine Vertretung braucht Rechte ohne Satzungsfunktion.

### Laufbahn: Übertritte, Ehrungen, Beförderungen (`Domain/Laufbahn`)

- **Übertritt, Probezeit, Ausscheiden** ändern nie eine Mitgliedschaft, sondern beenden sie am Vortag und legen eine neue an (`Mitgliedschaftswechsel`). Altersgrenzen aus Parametern: Aufnahme Einsatzabteilung ab 17, Einsatzdienst ab 18, Ende Jugendfeuerwehr mit 18, Altersabteilung ab 55, Dienstende 65, Probezeit 12 Monate, Vorlauf der Hinweise 3 Monate.
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

---

## 5. Qualifikationen und Lehrgänge

### Qualifikation (Katalog)

| Feld | Hinweis |
|---|---|
| `Code`, `Bezeichnung` | z. B. `TM1`, `TM2`, `SPRF`, `AGT`, `MA`, `TF`, `GF`, `ZF`, `GW`, `CSA`, `FS-C` |
| `Art` | `Lehrgang`, `Fahrerlaubnis`, `Sonstiges` |
| `GueltigkeitMonate` | `int?`, leer = unbefristet |
| `Fe2Funktion` | `string?`, Kürzel für alamos `osFunctions` (z. B. `AGT`, `MA`); nur **gültige** Qualifikationen werden übertragen |
| `Ebene` | `Standort`, `Kreis`, `Land` |

### QualifikationNachweis

| Feld | Hinweis |
|---|---|
| `MitgliedId`, `QualifikationId` | |
| `ErworbenAm` | `DateOnly` |
| `GueltigBis` | `DateOnly?`, vorbelegt aus `GueltigkeitMonate`, überschreibbar |
| `Lehrgangsort`, `Stunden`, `Bemerkung` | |
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

Möglich bis `Dienst.Rueckmeldung.SchlussStunden` vor Beginn. Löschfristen (Vorschlag, DSB-Freigabe offen, Löschlauf folgt):
Begründung nach `Dienst.Rueckmeldung.BegruendungLoeschenMonate` (3), Rückmeldung nach `Dienst.Rueckmeldung.LoeschenMonate` (12) Monaten nach dem Dienst. Führungskräfte übernehmen Zusagen als „anwesend“
und Absagen als „entschuldigt“ in die Anwesenheit, wo noch nichts erfasst ist.

### Gruppe (Katalog) und GruppenMitgliedschaft

Gruppen innerhalb der Abteilungen (z. B. Gruppe 1, Führungsgruppe) als Zielgruppe für Dienste.

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

Seed für Entwicklung: HLF 10 (43, 2023), TLF 8/18 (21, 1986), MTW (19, 1999), Schlauchanhänger (1999).

### Geraeteart (Katalog) und PruefplanPosition

`Geraeteart`: `Bezeichnung` (z. B. "Hydraulisches Rettungsgerät", "Steckleiter", "Haltegurt"), `AussonderungNachJahren?`, `PruefpflichtNach` (Text, z. B. "DGUV 305-002 Tab. 1").

`PruefplanPosition` (n je Geräteart): `Pruefart` (`Sichtpruefung`, `Funktionspruefung`, `Belastungspruefung`, `NachGebrauch`, `Hersteller`, `HU`, `UVV`), `IntervallMonate`, `Checkliste` (`jsonb`: Liste von Prüfpunkten mit Typ Ja/Nein, Messwert, Text). JSONB ist hier bewusst gewählt: Checklisten ändern sich, und jedes Protokoll speichert den Stand, mit dem geprüft wurde.

### Geraet

`Inventarnummer` (unique), `GeraeteartId`, `Bezeichnung`, `Hersteller`, `Typ`, `Seriennummer`, `Baujahr`, `IndienstAm`, `AusgesondertAm`, `FahrzeugId?` (verlastet auf), `Lagerort`, `QrCode` (= Inventarnummer, später Etikett).

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

`GeraetId?`/`FahrzeugId?`, `GemeldetVon`, `GemeldetAm`, `Beschreibung`, `Schwere` (`Info`, `Einschraenkung`, `AusserBetrieb`), `Status` (`Offen`, `InArbeit`, `Behoben`), `BehobenAm`, `PruefungId?`. Im Gerüst nur das Modell, der Workflow kommt später.

---

## 10. Benutzer, Rollen und Rechte

`Benutzer` ist der Identity-User (`IdentityUser<Guid>`), mit optionalem Verweis `MitgliedId` (1:1, eindeutig, beim Löschen des Mitglieds `NULL`). Getrennt, weil nicht jedes Mitglied einen Login hat (Jugend, Altersabteilung) und ein Gemeinde-Lesezugang kein Mitglied ist. Dazu `LetzteAnmeldungAm` und `DeaktiviertAm` (Konten werden deaktiviert statt gelöscht). Änderungen an Konten und Rollenvergaben schreibt der Audit-Interceptor mit, Passwort-Hash und Sicherheitsstempel nur als „geändert“.

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

Umsetzung: Rollen werden **nicht direkt** in den Seiten geprüft, sondern über **Policies** (`Mitglieder.Lesen`, `Mitglieder.Bearbeiten`, `Atemschutz.Lesen`, `Einsatz.Freigeben`, `Geraete.Pruefen`, …). Die Zuordnung Policy → Rollen steht an einer Stelle (`Berechtigungen.cs`). Datenbereiche (Jugendwart sieht nur JF) laufen über einen Resource-Based-Authorization-Handler.

---

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
