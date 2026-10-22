# Dokumentenablage, Ausschuss und Wahlen

Stand: 22.10.2026. Ergänzt `Datenmodell.md` um zwei Module.

## Dokumentenablage

| Entität | Zweck |
|---|---|
| `Dokumentkategorie` (Katalog) | Satzung und Ordnungen, Dienstanweisung, Protokoll, Merkblatt, Formular, Sonstiges |
| `Dokument` | Titel, Beschreibung, Kategorie, `Sichtbarkeit` (`Alle` oder `Fuehrung`), `LesebestaetigungErforderlich`, `GueltigAb`, `AufgehobenAm` |
| `DokumentVersion` | laufende `Nummer` je Dokument, Dateiname, Inhaltstyp (aus der Endung), Größe, SHA-256, Änderungshinweis. Versionen werden nie überschrieben. |
| `DokumentDatei` | Inhalt als `bytea` in eigener Tabelle `dokument_dateien`, damit Listen ihn nicht laden. Inhalt steht nicht im Änderungsprotokoll. |
| `Lesebestaetigung` | Konto + Version + Zeitpunkt. Eine neue Version braucht eine neue Bestätigung. |

**Dateispeicher:** PostgreSQL (`IDateiablage` → `DatenbankDateiablage`). Abgestimmt mit dem Betriebskonzept:
das nächtliche `pg_dump` per restic sichert die Dateien mit, der Gerätehaus-PC hat sie über die Replikation
auch offline, und es gibt kein zweites Volume. Erwartet sind wenige hundert Dateien unter 1 GB.
Größenlimit im Parameter `Dokument.Datei.MaxMegabyte` (20), technische Obergrenze einer Anfrage 100 MB.

**Sicherheit:** erlaubt sind PDF, Office (docx/xlsx/pptx/odt/ods/doc/xls), txt, png, jpg. Der Inhaltstyp kommt
aus der Endung, nie vom Browser. PDF, Bilder und Text werden mit `nosniff`, `no-store` und einer
Sandbox-CSP im Browser angezeigt, alles andere nur als Download.

**Rechte:** `Dokumente.Lesen` alle Rollen, `Dokumente.VertraulichLesen` Kommandant, Stellvertreter,
Führungskraft, Schriftführer; `Dokumente.Verwalten` Kommandant, Stellvertreter, Schriftführer. Den
Bestätigungsstand (wer fehlt noch) sehen nur Verwaltende; gezählt werden aktive Konten mit Leserecht.

## Ausschuss, Hauptversammlung und Wahlen

| Entität | Zweck |
|---|---|
| `Sitzung` | Art (Ausschuss, Hauptversammlung, Abteilungsversammlung, Jugendausschuss, Sonstige), Beginn/Ende, Ort, `EingeladenAm`, Tagesordnung, Niederschrift (Text), Protokollführer, unterschriebener Scan als `Dokument`, Status, `ProtokollGenehmigtAm`, `DemBuergermeisterVorgelegtAm` |
| `SitzungTeilnahme` | Mitglied, anwesend/entschuldigt/unentschuldigt, stimmberechtigt |
| `Beschluss` | Kennung `JJJJ/NN` (laufend je Jahr, gelöschte Nummern werden nicht neu vergeben), TOP, Gegenstand, Ergebnis, Ja/Nein/Enthaltungen, `UmsetzenBis`, `ErledigtAm` |
| `Wahl` | Funktion, Wahlgang, Verfahren, geheim, Sitze, Stimmberechtigte, Stimmzettel, gültige Stimmen, Amtsbeginn, Amtszeit, Zustimmung Gemeinderat, Status |
| `WahlBewerber` | Mitglied, Stimmen, Platz, gewählt, Annahme der Wahl |

**Regeln (FwG BW, Mustersatzung):**

Quelle: Feuerwehrsatzung Adelberg vom 26.02.2015 (§§ 10, 13, 14, 15).

- Einladungsfrist: Hauptversammlung 14 Tage (`Gremien.Hauptversammlung.EinladungsfristTage`, § 14 Abs. 3),
  Ausschuss 3 Tage (`Gremien.Ausschuss.EinladungsfristTage`, § 13 Abs. 4).
- Beschlussfähig ab der Hälfte der Stimmberechtigten; bei der Hauptversammlung bezogen auf alle Angehörigen der
  Einsatzabteilung (§ 14 Abs. 4; eine zweite Hauptversammlung ist ohne Rücksicht darauf beschlussfähig).
  Einfache Mehrheit, Stimmengleichheit gilt als abgelehnt (§ 13 Abs. 6).
- Kommandant und Stellvertreter (§ 15 Abs. 3): mehr als die Hälfte der Stimmen der anwesenden Wahlberechtigten,
  sonst Stichwahl der beiden Ersten mit einfacher Mehrheit, bei Gleichstand Los. Bei nur einem Bewerber braucht auch
  der zweite Wahlgang diese Mehrheit. Ohne eingetragene Zahl der Anwesenden stellt FEVAD keine absolute Mehrheit fest;
  zur Stichwahl stehen nur zwei Bewerber. Die Beschlussfähigkeit der Hauptversammlung zählt die Einsatzabteilung am Sitzungstag. Einspruch und Vorlage der Niederschrift an den Bürgermeister jeweils binnen einer Woche.
  Bestellung durch den Bürgermeister nach Zustimmung des Gemeinderats; erst mit eingetragener Zustimmung
  lässt sich die Wahl übernehmen. Vorgabe kommt aus dem Funktionskatalog (`VorgeschlageneRolle`).
- Ausschuss und andere Mehrheitswahlen: die Bewerber mit den meisten Stimmen; die übrigen sind Ersatzmitglieder.
  Gleichstand um den letzten Sitz bleibt offen (Los), die Wahlleitung setzt „gewählt“ dann von Hand.
- Übernehmen legt je Gewähltem mit Annahme eine `FunktionZuordnung` (`Art = Wahl`) an:
  `GueltigAb` = Amtsbeginn, `GueltigBis` = Amtsbeginn + Amtszeit − 1 Tag, `Beschlussdatum` = Sitzungstag.
  Optional endet die Amtszeit der bisherigen Inhaber am Tag vor dem Amtsbeginn.
- Übersicht „Ämter und Amtszeiten“ warnt `Gremien.Amtszeit.WarnungTage` (180) vor dem Ende und zeigt
  Funktionen mit Amtszeit ohne laufende Besetzung.

**Rechte:** `Gremien.Lesen` Kommandant, Stellvertreter, Führungskraft, Schriftführer, Kassier;
`Gremien.Bearbeiten` Kommandant, Stellvertreter, Schriftführer; `Gremien.WahlenUebernehmen` Kommandant, Stellvertreter.

## Offen

- Sollen einfache Mitglieder die Niederschrift der Hauptversammlung sehen? Heute nur über ein Dokument mit Sichtbarkeit „alle“.
- Später: Einladung per E-Mail mit Tagesordnung, PDF der Niederschrift, Wählerverzeichnis aus der Einsatzabteilung,
  Verknüpfung der Hauptversammlung mit dem Dienstkalender.
