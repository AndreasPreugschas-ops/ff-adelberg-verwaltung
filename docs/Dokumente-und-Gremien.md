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

- Einladungsfrist: Hauptversammlung 14 Tage (`Gremien.Hauptversammlung.EinladungsfristTage`, Satzung Adelberg),
  Ausschuss 7 Tage (`Gremien.Ausschuss.EinladungsfristTage`, vorläufig aus der Mustersatzung).
- Beschlussfähig ab der Hälfte der Stimmberechtigten. Einfache Mehrheit, Stimmengleichheit gilt als abgelehnt.
- Kommandant und Stellvertreter: absolute Mehrheit der gültigen Stimmen, sonst Stichwahl (neuer Wahlgang).
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

- Einladungsfrist des Ausschusses in der Satzung Adelberg prüfen (vorläufig 7 Tage).
- Sollen einfache Mitglieder die Niederschrift der Hauptversammlung sehen? Heute nur über ein Dokument mit Sichtbarkeit „alle“.
- Später: Einladung per E-Mail mit Tagesordnung, PDF der Niederschrift, Wählerverzeichnis aus der Einsatzabteilung,
  Verknüpfung der Hauptversammlung mit dem Dienstkalender.
