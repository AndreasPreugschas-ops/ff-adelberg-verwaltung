# Gesamtkonzept FEVAD

**Stand:** 05.11.2026, nach den Umsetzungspaketen P1 bis P7 und PR #145 („Gemeindeverwaltung“ statt „Hauptamt“).
**Version:** im Aufbau (`0.x`), noch keine Echtdaten.

## Kurzfassung

FEVAD (**FE**uerwehr, **V**erwaltung, **AD**elberg) ist die Verwaltungssoftware der Freiwilligen Feuerwehr Adelberg
(Baden-Württemberg). Sie ersetzt Listen, Ordner und MP-Feuer durch eine Webanwendung, die auf dem Handy wie am PC
läuft: Mitglieder und Laufbahn, Ausbildung, Atemschutz, Dienste, Einsatzberichte, Technik, Kleiderkammer, Kassen,
Entschädigung, Kostenersatz, Jugendfeuerwehr, Gremien und Datenschutz. Alarmierung und Einsatzführung gehören nicht dazu.

Fachlich sind alle sieben Pakete umgesetzt. Vor dem Echtbetrieb fehlen keine Programmteile mehr, sondern Entscheidungen
und Verträge: Freigabe durch Bürgermeister und Datenschutzbeauftragten, Auftragsverarbeitungsverträge, Hoster, Domain
und Mailkonto. Für den Betrieb gibt es zwei Stufen bei Hetzner:

| Stufe | Wofür | Laufend | Einmalig |
|---|---|---|---|
| **Klein** | **Minimum für den Go-Live** | ≈ 11,65 €/Monat (≈ 145 €/Jahr) | ≈ 75 € USB-Platte für die Monatskopie |
| **Mittel** ★ | **empfohlene Ausbaustufe** | ≈ 21,69 €/Monat (≈ 265 €/Jahr) | ≈ 450 € Notbetrieb-PC mit USV |

Alle Beträge brutto, Richtwerte vom 05.11.2026, keine Angebote (Herleitung: Abschnitt [Betrieb und Hosting](#7-betrieb-und-hosting)).

## 1. Ziel und Grundsätze

- **Weniger Papier, weniger Fristenstress, mehr Rechtssicherheit.** Jede Frist (Atemschutz, Prüfungen, Lehrgänge,
  Ehrungen, Löschungen) erinnert von selbst; die Startseite „Was liegt an“ zeigt jedem nur seine Aufgaben.
- **Ehrenamt zuerst:** wenige Pflichtfelder, verständliche Sprache, Bedienung ohne Schulung, alles auch auf dem Handy
  im Gerätehaus um 22:30 Uhr nach dem Einsatz. Wer kein Smartphone hat, wird nie eingeschränkt.
- **Konfiguration statt Hartkodierung:** Fristen, Sätze, Altersgrenzen und Schwellen stehen als `Parameter` oder
  Katalogwert mit Quelle in den Stammdaten, nie fest im Code. Feuerwehrrecht ist Landesrecht; Vorbelegung ist
  Baden-Württemberg und die Satzungen der Gemeinde Adelberg.
- **Datensparsamkeit:** erhoben wird, was eine Aufgabe braucht. Gesundheitsdaten (Atemschutz) nur als Status und
  Frist, nie Befunde. Daten Minderjähriger strikt abgeschirmt.
- **Nachvollziehbarkeit:** jede Änderung im Änderungsprotokoll, Lesezugriffe auf sensible Daten im Leseprotokoll,
  Löschungen mit Löschnachweis.
- **Offen und günstig:** Open Source (EUPL-1.2), keine Lizenzkosten, Betrieb auf einem kleinen Mietserver in Deutschland.

## 2. Umfang

### Was FEVAD macht

| Bereich | Inhalt (Auswahl) | Paket |
|---|---|---|
| Mitglieder und Laufbahn | Akte, Mitgliedschaften als Zeiträume, Dienstgrade, Funktionen, Übertritte, Ehrungen, Beförderungsvorschläge, Dienstpflicht-Vorgänge, Aufnahme- und Änderungsanträge, Dienstausweise | Grundstock, P1, P5 |
| Ausbildung | Qualifikationen, Lehrgänge mit Anträgen und Wünschen, Nachweise mit Ablauf, Ausbildungsmatrix, Übungsplanung, Stundenkonten, Unterweisungen | Grundstock, P3, P5 |
| Atemschutz | Vorsorge (nur Status/Frist), Belastungsübung, Unterweisung, Einsatz- und Übungsnachweise mit Bestätigung durch Kommandant und Atemschutzwart, Atemschutztechnik | P1, P6 |
| Dienste | Dienste und Serien, Rückmeldungen, Anwesenheit, Abwesenheit mit Entschuldigung, iCal-Abo | Grundstock, P1 |
| Einsätze | Bericht aus alamos FE2 als Entwurf, Freigabe, Einsatzleitung/Tagebuch, Nachbesprechung, Ausgaben als PDF, Archivanbietung und Verdichtung | Grundstock, P5, P7 |
| Kostenersatz | Vorgang von der Empfehlung der Feuerwehr bis zur Entscheidung der Gemeindeverwaltung, Bescheid-Statuskette, BMA-Betreiber, Kalkulation | P3 |
| Entschädigung | Sätze nach Entschädigungssatzung 2018, Auszahlungslisten, Arbeitgebererstattung mit Prüfcode, Jahresbescheinigungen | P3 |
| Technik | Fahrzeugakte, Beladung, Geräte und Prüfungen, Mängel, Fahrtenbuch, Schlauchbuch, Verbrauchsmaterial, Inventur, Beschaffung, Haushaltsanmeldung, Jahresbericht Gerätewart | Grundstock, P1, P6 |
| Kleiderkammer | Ausgabe, Tausch, Rücknahme, Größen, Lieferanten mit Preisen, Nachbedarf | P6 |
| Kassen | Kameradschaftskasse und JF-Kasse, Wirtschaftsplan, Kassenbuch, Kontoauszug-Import (CAMT.053/CSV), Auslagen, Spenden, Bestandsverzeichnis, Jahresabschluss, Kassenprüfung | P2 |
| Jugendfeuerwehr | Einwilligungen, Notfallangaben, Betreuer mit Führungszeugnis-Datum, Elternkonten, Warteliste, Jugendveranstaltungen, Schutzkonzept-Prüftermin | P4 |
| Führung und Gremien | Jahreskreis, Kennzahlen, Ausschuss und Hauptversammlung mit Niederschrift und Beschlussregister, Jahresbericht, Chronik, Serienbriefe, Quartalsbericht Bürgermeister | P5 |
| Kommunikation | Benachrichtigungen, Mitteilungen, Elternpost, Rundfragen, Kontaktliste (nur mit Einwilligung) | P7 |
| Gemeinde | Rollen Gemeindeverwaltung, Kämmerei und Abgaben, Gemeinde-Vorgänge, Mitgliederbescheide, Finanzexport für SAP | P3 |
| Datenschutz und Betrieb | Löschlauf mit Löschregeln, Selbstauskunft, Gesamtexport, Vier-Augen-Rechtefreigabe, Leseprotokoll, Altdatenimport, Betriebsseite, Papier-Notfallsatz, Barrierefreiheit | P1, P4, P7 |
| Schnittstellen | alamos FE2 (Alarm → Bericht, Personenabgleich), iCal, CSV für ATLAS, PDF/CSV an die Gemeinde | Grundstock, PR #142 |

### Was FEVAD bewusst nicht macht

- **Keine Alarmierung, keine Einsatzführung, keine Verfügbarkeitsanzeige.** Das bleibt bei Leitstelle, digitalem
  Meldenetz und alamos. FEVAD übernimmt Alarmdaten nur nachträglich.
- **Keine medizinischen Befunde**, keine Tauglichkeitsbewertung.
- **Kein Förderverein**, keine Lohnabrechnung, kein Ersatz für SAP der Kämmerei (nur Export).
- Nicht gebaut, aber vorgemerkt: Notbetrieb Stufe 2 (V67, Entscheidung nach Messung), Web-Push, mehrere Gemeinden in
  einer Instanz (V73), Beförderungsvoraussetzungen der Anlage 2 VwV vom 27.09.2022, Ordnungsmaßnahmen Jugend.

## 3. Rollen und Rechte

Seiten prüfen **Rechte** (Policies), nicht Rollen. Die Zuordnung Rolle → Recht steht in
`Infrastructure/Identitaet/Berechtigungen.cs` und lässt sich unter **Verwaltung › Rechte** anpassen. Rechte, die
Gesundheits- oder Jugenddaten öffnen, wirken erst nach Freigabe durch eine zweite Person (Vier-Augen-Prinzip).

| Rolle | Kurz | Zwei-Faktor-Pflicht |
|---|---|---|
| Administrator | Konten, Rechte, Betrieb, Gesamtexport | ja |
| Kommandant, Stellvertreter | Führung, Freigaben, Personal | ja |
| Führungskraft | Dienste und Einsätze der eigenen Abteilung | nein |
| Gerätewart, Atemschutzgerätewart, Kleiderwart | Technik, Atemschutztechnik und -nachweise, Kleiderkammer | ja |
| Jugendwart, Betreuer Jugend | Jugendfeuerwehr, abgeschirmt von allen anderen; Betreuer erst nach Verpflichtung | ja |
| Kassier, Schriftführer | Kassen, Gremien, Vorlagen | ja |
| Kassenprüfer | aus dem Prüfauftrag abgeleitet, zeitlich begrenzt | ja |
| Ausschussmitglied | aus der Funktion abgeleitet, liest den Haushaltsentwurf | nein, solange dieser keine Namen enthält |
| Gemeinde: Verwaltung (Rathaus), Kämmerei, Abgaben, Gemeinde lesen | Kostenersatz, Bescheide, Haushalt, Finanzexport; wirken erst nach Verpflichtung auf Vertraulichkeit | ja |
| Mitglied | eigene Akte, Rückmeldungen, Anträge, Meldungen | nein |
| Erziehungsberechtigt | nur Termine und Post des eigenen Kindes | nein |

Im Code heißt die Rathaus-Rolle `Rollen.Gemeindeverwaltung`; der technische Rollenname in der Datenbank bleibt
„Hauptamt“, damit bestehende Zuordnungen gültig bleiben. In Oberfläche und Unterlagen wird „Hauptamt“ nicht verwendet.
Das vollständige Rollen- und Berechtigungskonzept steht in den Datenschutz-Unterlagen (Dokument 03).

## 4. Datenschutz

- **Verantwortlich** ist die Gemeinde Adelberg; die Feuerwehr ist eine Einrichtung der Gemeinde. Rechtsgrundlage für
  Pflichtaufgaben ist Art. 6 Abs. 1 lit. c und e DSGVO mit FwG BW, Einwilligung nur für Freiwilliges (Fotos,
  Kontaktliste, Geburtstage außerhalb der Führung).
- **Unterlagen** (Projektordner `datenschutz/`, gepflegt im Thread „Datenschutz-Unterlagen“): Verzeichnis der
  Verarbeitungstätigkeiten (01), TOM (02), Rollen- und Berechtigungskonzept (03), Löschkonzept mit Regeln L1–L69 (04),
  Gesundheitsdaten Atemschutz (05), Datenschutzinformation (06), AVV-Checkliste Hosting (07),
  DSFA-Schwellwertprüfung (08) und DSFA-Entwurf (12).
- **Im Programm:** Löschlauf jede Nacht mit Löschnachweis, Selbstauskunft nach Art. 15 als PDF, Gesamtexport nur für
  den Administrator mit Bestätigung, Änderungs- und Leseprotokoll, Archivanbietung vor dem Löschen.
- **Vor Echtdaten:** Freigabe durch Bürgermeister und Datenschutzbeauftragten, AVV mit Server- und Sicherungsanbieter
  (Vertragspartner Gemeinde), DSB-Freigabe der Löschregeln L45–L50, Freigabevermerk vor der Altdatenübernahme.

## 5. Technik

| Baustein | Wahl |
|---|---|
| Laufzeit | .NET 10 (LTS), SDK in `global.json` |
| Oberfläche | ASP.NET Core Razor Pages, HTMX, Alpine.js, Tailwind CSS (ohne Node, Dateien lokal, kein CDN) |
| Datenbank | PostgreSQL 17 über EF Core, Tabellen in snake_case, Dokumente als `bytea` in der Datenbank |
| Anmeldung | ASP.NET Core Identity, TOTP-2FA und Passkeys, Einrichtungslinks statt Selbstregistrierung |
| Ausgaben | PDF (QuestPDF), DOCX und XLSX, CSV |
| Tests | xUnit, Testcontainers (PostgreSQL), Playwright mit axe-core für Barrierefreiheit |
| Betrieb | Docker Compose mit Caddy (HTTPS), Image in GHCR, Skripte in `deploy/` |
| Dokumentation | Handbuch (`docs/`) und API aus den XML-Kommentaren mit DocFX (siehe [Dokumentationsrichtlinie](Dokumentationsrichtlinie.md)) |

Lösungsstruktur:

```
src/Fevad.Domain          Entitäten und Regeln, ohne Abhängigkeiten
src/Fevad.Infrastructure  EF Core, Migrationen, Identity und Rechte, Löschlauf, PDF, Mail, Import, Hintergrunddienste
src/Fevad.Web             Razor Pages, HTMX-Partials, FE2-Webhook, iCal
tests/…                   Domänen- und Integrationstests (inkl. Oberflächentests)
deploy/                   Server: Compose, Caddy, Sicherung, Restore-Test, Update, Wächter
docfx/                    DocFX-Gerüst für die Dokumentationsseite
```

Datenmodell und Entscheidungen im Einzelnen: [Datenmodell](Datenmodell.md).

## 6. Fristen und Parameter

Alle Werte mit Rechts- oder Ortsbezug sind `Parameter` mit Gültigkeit und Quellenangabe (Stammdaten › Parameter) oder
Katalogwerte. Beispiele: Entschädigungssätze (`Entschaedigung.*`), Kasse (`Kasse.*`), Atemschutz-Jahrespflichten
(`Atemschutz.Jahrespflicht.WarnungAbMonat`), Löschfristen (`Datenschutz.*`), Betriebsschwellen (`Betrieb.*`).
Vorbelegungen ohne gesicherte Quelle tragen den Vermerk „Satz fehlt“ oder „Platzhalter“ und sperren dort, wo ein
falscher Wert Schaden anrichtet (Kostenersatz, solange die Kostenersatzsatzung fehlt).

## 7. Betrieb und Hosting

### Betriebsmodell

**Cloud primär:** ein gemieteter Server in Deutschland betreibt Anwendung, Datenbank und HTTPS. Das Gerätehaus braucht
dafür nur einen Browser. Fällt dort das Internet aus, hilft der Papier-Notfallsatz (Stufe „Klein“) oder ein
Notbetrieb-PC mit Lesekopie (Stufe „Mittel“, sobald Stufe 2 gebaut ist). Die Einrichtung beschreibt das
[Betriebshandbuch](Betrieb.md); die Dateien aus `deploy/` funktionieren bei jeder Stufe gleich.

Grundlage ist der Hosting-Vergleich vom 05.11.2026 (15 Varianten bei Hetzner, IONOS, STRATO, netcup, Komm.ONE und im
Eigenbetrieb; Projektordner `hosting/`). Für FEVAD mit rund 100 Mitgliedern und wenigen gleichzeitigen Nutzern
reichen 2 vCPU und 4 GB RAM. Hetzner ist in beiden Stufen am günstigsten bei gleicher Leistung, bietet den AVV im
Kundenkonto an und hat zwei deutsche Rechenzentren (Nürnberg, Falkenstein).

### Stufe „Klein“: Minimum für den Go-Live

| Baustein | Lösung | €/Monat brutto |
|---|---|---|
| Server | Hetzner Cloud CX23: 2 vCPU, 4 GB RAM, 40 GB NVMe, Nürnberg | 6,53 |
| Server-Backup | Hetzner Backup-Option (+20 %, 7 tägliche Abbilder) | 1,31 |
| Sicherung extern | restic täglich, verschlüsselt, auf Hetzner Storage Box BX11 (1 TB) in Falkenstein | 3,81 |
| Zweite Kopie | einmal im Monat `restic copy` auf eine verschlüsselte USB-Platte, die im Rathaus liegt | 0 (≈ 75 € einmalig, Schätzwert) |
| Mail | Funktionspostfach der Gemeinde (SMTP) | 0 |
| Überwachung | healthchecks.io kostenlos, Mail an zwei Betreuer | 0 |
| Domain, Zertifikat | Subdomain der Gemeinde oder `.de` (≈ 5 €/Jahr); Let's Encrypt über Caddy | ≈ 0,40 |
| Notbetrieb | Papier-Notfallsatz (gebaut, monatlicher Druck) | 0 |
| Software | Ubuntu 24.04 LTS, Docker, PostgreSQL 17, Caddy, restic, ufw; alles Open Source | 0 |
| **Summe** | | **≈ 11,65 €/Monat, ≈ 145 €/Jahr** |

**Warum das für den Start reicht:** Brand, Diebstahl oder Wasser im Gerätehaus betreffen die Daten nicht mehr;
Plattenausfall und Rechenzentrum fängt der Anbieter ab; gegen Trojaner und versehentliches Löschen gibt es tägliche,
verschlüsselte Stände (14 täglich, 8 wöchentlich, 12 monatlich) in einem anderen Rechenzentrum, wöchentlich
automatisch zurückgespielt und geprüft. Wiederanlauf nach Totalverlust ≈ 2 Stunden, Pflege ≈ 1 Stunde im Monat
(Erfahrungswerte, nicht gemessen).

**Grenzen von „Klein“:** Sicherung liegt beim selben Anbieter wie der Server (gemildert durch die USB-Monatskopie,
Datenverlust dann bis zu einem Monat, wenn das Hetzner-Konto ganz verloren geht); kein Lesezugriff im Gerätehaus bei
Internetausfall (Papier); CX23 ist bei Hetzner als „begrenzt verfügbar“ markiert. Ist CX23 ausverkauft, gleich CX33
(Mittel) nehmen, nicht CPX22 (≈ 23 €/Monat).

**Pflicht vor dem Go-Live auch bei „Klein“:** AVV mit Hetzner (Server und Storage Box) durch die Gemeinde; zweiter
Zugang beim Kommandanten (Kundenkonto mit 2FA, SSH-Schlüssel, Domain); `RESTIC_PASSWORD` versiegelt im Rathaus;
Release-Checkliste im Betriebshandbuch vollständig abgehakt.

### Stufe „Mittel“: empfohlene Ausbaustufe

| Baustein | Änderung gegenüber „Klein“ | €/Monat brutto |
|---|---|---|
| Server | Hetzner Cloud CX33: 4 vCPU, 8 GB RAM, 80 GB NVMe (Reserve für Dokumente und Updates) | 10,10 |
| Server-Backup | +20 % | 2,02 |
| Sicherung extern | Storage Box BX11 in Falkenstein (Server in Nürnberg) | 3,81 |
| Zweitkopie | täglich automatisch bei einem zweiten Anbieter, z. B. STRATO HiDrive S3 (`ZWEITKOPIE_REPOSITORY`) | ≈ 1,00 |
| Mail | eigenes Funktionspostfach, z. B. mailbox.org Business, nur falls die Gemeinde keines stellt | 4,76 |
| Überwachung | healthchecks.io kostenlos | 0 |
| Notbetrieb | Mini-PC als Lesespiegel im Gerätehaus mit kleiner USV, vollverschlüsselt, abschließbar | ≈ 450 € einmalig (Schätzwert) |
| **Summe** | | **≈ 21,69 €/Monat, ≈ 265 €/Jahr + ≈ 450 € einmalig** |

**Was „Mittel“ zusätzlich bringt:** echte Anbieter-Trennung der Sicherung (übersteht auch den Verlust des
Hetzner-Kontos mit höchstens einem Tag Datenverlust), doppelte Leistung für Dokumente, Berichte und Updates, und mit
Stufe 2 einen Lesezugriff auf Kerndaten im Gerätehaus bei Internetausfall. Wiederanlauf ≈ 1–2 Stunden, Pflege
≈ 1–2 Stunden im Monat.

**Wann ausbauen:** sobald der Haushalt es zulässt, spätestens wenn eines davon eintritt: die Messung am Gerätehaus
(Betriebshandbuch, F10) zeigt Ausfälle über einer Stunde; die Datenbank mit Dokumenten wächst über 20 GB; der Server
ist im Dienstbetrieb spürbar langsam; der Datenschutzbeauftragte verlangt die Anbieter-Trennung der Sicherung.

**Umstieg von „Klein“ auf „Mittel“** geht ohne Neuinstallation: in der Hetzner-Konsole den Servertyp auf CX33 ändern
(wenige Minuten Ausfall, außerhalb der Dienstzeiten), Zweitkopie in `.env` eintragen, Notbetrieb-PC beschaffen, wenn
Stufe 2 gebaut ist. Schritte im [Betriebshandbuch](Betrieb.md#ausbaustufen-klein-und-mittel).

### Vergleich mit heute und den anderen Varianten

| Kriterium | Heute (MP-Feuer im Magazin) | Klein | Mittel |
|---|---|---|---|
| Brand, Wasser, Diebstahl im Gerätehaus | Totalverlust | nicht betroffen | nicht betroffen |
| Verschlüsselungstrojaner, Löschen | keine Historie | tägliche Stände | tägliche Stände, zwei Anbieter |
| Verlust des Anbieterkontos | – | bis 1 Monat Verlust (USB) | ≤ 1 Tag Verlust |
| Internet im Gerätehaus fällt aus | läuft lokal | Papier-Notfallsatz | Lesespiegel (Stufe 2) |
| Zugriff von zuhause, Handy | kaum | überall | überall |
| Eigentum und Datenschutz | privates Gerät | AVV der Gemeinde | AVV der Gemeinde |

Verworfen: „Teuer“ (dedizierte Kerne, ≈ 89 €/Monat + 1.750 €) ist für FEVAD überdimensioniert; IONOS und STRATO
sind in beiden Stufen teurer und gehören zur selben Gruppe; netcup bindet 12 Monate und hat 2026 stark erhöht;
Eigenbetrieb im Gerätehaus hängt am schwachen Internet in Adelberg und erbt die Risiken des Magazins; Komm.ONE
veröffentlicht keine Preise (Anfrage nur über die Gemeinde, siehe H2).

### Offene Entscheidungen zum Hosting

| Nr. | Frage | Empfehlung | Wer entscheidet |
|---|---|---|---|
| H1 | Anbieter und Stufe | Hetzner; Go-Live mit „Klein“, Ausbau auf „Mittel“ | Bürgermeister auf Vorschlag des Kommandanten |
| H2 | Komm.ONE vorher anfragen? | ja, kurze Anfrage der Gemeinde | Gemeindeverwaltung (Rathaus) |
| H3 | Mailpostfach | Funktionspostfach der Gemeinde, sonst mailbox.org | Gemeindeverwaltung (Rathaus) |
| H4 | Sofortsicherung MP-Feuer | jetzt einrichten, unabhängig von FEVAD | Kommandant |
| H5 | Notbetrieb-PC (Stufe 2) | erst nach der Internet-Messung, Budget 450 € vormerken | Kommandant, Haushalt |
| H6 | AVV für Server und Sicherung | online bei beiden Anbietern, Vertragspartner Gemeinde | Bürgermeister, DSB |

## 8. Qualität und Entwicklung

- Jeder PR läuft durch die CI: Build mit Warnungen als Fehler, Paketprüfung auf Sicherheitslücken, Prüfung auf
  fehlende Migrationen, alle Tests (auch Oberflächen- und Barrierefreiheitstests), Betriebsdateien (shellcheck,
  Compose, Caddy) und die Dokumentation (DocFX mit Warnungen als Fehler).
- Vollständige deutsche XML-Kommentare sind Pflicht; fehlende Kommentare an öffentlichen Membern brechen den Build.
- Testdaten sind erfunden und erkennbar (Personalnummern `T-…`), nur in der Entwicklung.
- Releases nach Semantic Versioning über GitHub-Releases (README, „Versionen und Releases“).

## 9. Weg zum Echtbetrieb

1. Gespräch mit Bürgermeister, Gemeindeverwaltung und DSB: Auftrag, Hosting (H1–H6), Datenschutz-Unterlagen 00, 08, 12.
2. Pilotphase mit 3–5 Funktionsträgern auf Stufe „Klein“ mit Testdaten (empfohlen vor dem Release).
3. Hetzner-Konto der Gemeinde, AVV, Server und Storage Box, Domain, Mailkonto; Einrichtung nach Betriebshandbuch.
4. Release `1.0.0`, Release-Checkliste, Altdatenübernahme aus MP-Feuer mit Freigabevermerk.
5. Messung am Gerätehaus (vier Wochen), danach Entscheidung über Stufe 2 und Ausbau auf „Mittel“.

Zurückgestellte offene Punkte aller Bereiche sammelt der Projektordner unter `anforderungen/Offene Punkte FEVAD.md`.

## Selbstprüfung

- **Landesrecht:** Entschädigung, Kostenersatz, Dienstgrade, Ehrungen und Altersgrenzen folgen BW und den Adelberger
  Satzungen; alle Werte sind Parameter. Die Kostenersatzsatzung fehlt noch (Platzhalter mit Sperre). Hosting selbst ist
  nicht landesrechtlich geregelt; ob Vergabe- oder Komm.ONE-Bindungen greifen, klärt die Gemeindeverwaltung.
- **Datensparsamkeit:** Sicherungen sind vor dem Hochladen verschlüsselt, die Anbieter sehen nur Chiffrat; die
  Überwachung überträgt keine Personendaten.
- **Wer sieht zu viel:** Der Server-Administrator sieht technisch alles; deshalb Verpflichtung auf Vertraulichkeit
  (Vorlage 09) und zweiter Zugang beim Kommandanten.
- **Fristen:** Aufbewahrung der Sicherungen (14/8/12) steht im Löschkonzept; die USB-Monatskopie unterliegt
  denselben Fristen und wird nach 12 Monaten überschrieben.
- **22:30 Uhr nach dem Einsatz:** Mit dem Cloud-Server geht der Einsatzbericht vom Handy über Mobilfunk, auch wenn
  das Internet im Gerätehaus hängt.
