# Prüfbericht Barrierefreiheit (V66)

Selbstbewertung von FEVAD gegen EN 301 549 V3.2.1, Kapitel 9 (Web, entspricht WCAG 2.1 Stufe A und AA), 10 (Dokumente) und 12
(Dokumentation und Support). Rechtsgrundlage: BITV 2.0 §§ 2, 2a, 3 und 7, über § 10 Abs. 1 L-BGG BW für die Gemeinde entsprechend
anwendbar; § 14 Abs. 2 EGovG BW.

**Stand:** 26.10.2026, FEVAD nach Paket P7. **Ergebnis:** teilweise vereinbar. Vor dem Echtbetrieb fehlt die Prüfung durch eine
unabhängige Stelle (z. B. BITV-Test) und ein Test mit Menschen, die Screenreader oder Vergrößerung nutzen.

## Wie geprüft wird

| Verfahren | Umfang | Wo |
|---|---|---|
| Automatisch mit axe-core (Regeln `wcag2a`, `wcag2aa`, `wcag21a`, `wcag21aa`) | Startseite, alle Menüseiten und je Menüseite die verlinkten Unterseiten (bis 120 je Durchlauf) für Mitglied, Kommandant, Administrator, Kassier/Gerätewart/Jugendwart, dazu Kommandant im Dunkelmodus; zusammen rund 300 Seiten | `tests/Fevad.Integration.Tests/OberflaechenTests.cs`, läuft in der CI bei jeder Änderung |
| Automatisch im Browser | Umbruch bei 320 px Breite ohne waagrechtes Scrollen, Sprunglink, Fokus auf den Inhalt, Dunkelmodus nach Gerät und Auswahl, keine Skriptfehler | ebenda |
| Durchsicht des Codes | Formulare, HTMX-Austausch, Farben, PDF-Erzeugung | dieser Bericht |
| Offen | Screenreader (NVDA, VoiceOver, TalkBack), Sprachsteuerung, Prüfung durch Fachstelle | vor dem Echtbetrieb |

axe-core findet nach eigener Angabe rund die Hälfte der prüfbaren Fehler. „Erfüllt“ heißt unten: automatisch ohne Befund und in
der Durchsicht nichts Gegenteiliges gefunden.

## Kapitel 9: Web (WCAG 2.1 A und AA)

| Kriterium | Stufe | Ergebnis | Anmerkung |
|---|---|---|---|
| 1.1.1 Nicht-Text-Inhalt | A | teilweise | Bedienelemente mit Symbol haben Text oder `aria-label`. Hochgeladene Fotos (Mängel, Lichtbilder) haben nur den Dateinamen als Alternative. |
| 1.2.1–1.2.5 Zeitbasierte Medien | A/AA | nicht anwendbar | Keine Audio- oder Videoinhalte. |
| 1.3.1 Info und Beziehungen | A | erfüllt | Überschriften, Listen, Tabellen mit `<th>`, Beschriftungen an Feldern; von axe auf allen Seiten geprüft. |
| 1.3.2 Bedeutungstragende Reihenfolge | A | erfüllt | DOM-Reihenfolge = Lesereihenfolge, kein CSS-Umsortieren. |
| 1.3.3 Sensorische Eigenschaften | A | erfüllt | Hinweise beziehen sich auf Beschriftungen, nicht auf Form oder Lage. |
| 1.3.4 Ausrichtung | AA | erfüllt | Keine Sperre auf Hoch- oder Querformat. |
| 1.3.5 Zweck der Eingabe | AA | teilweise | `autocomplete` an Anmeldung und Konto; persönliche Daten in der Mitgliederakte noch ohne `autocomplete`-Werte. |
| 1.4.1 Benutzung von Farbe | A | erfüllt | Ampeln (Fristen, Status) tragen immer Text; Fehler mit Text unter dem Feld; Links im Fließtext sind unterstrichen. |
| 1.4.2 Audio-Steuerelement | A | nicht anwendbar | |
| 1.4.3 Kontrast (Minimum) | AA | erfüllt | axe ohne Befund im hellen und dunklen Farbschema. Grau 400 als Schriftfarbe dafür abgedunkelt, im Dunkelmodus Grau 400/500 aufgehellt. |
| 1.4.4 Textgröße ändern | AA | erfüllt | Schriftgrößen in rem, Zoom bis 200 % ohne Verlust. |
| 1.4.5 Bilder von Text | AA | erfüllt | Keine Textgrafiken außer Logos auf PDF. |
| 1.4.10 Umbruch (Reflow) | AA | teilweise | Seiten der Einsatzkraft bei 320 px geprüft; breite Tabellen (Ausbildungsmatrix, Haushalt) scrollen innerhalb der Tabelle, was für Datentabellen zulässig ist. |
| 1.4.11 Kontrast von Nicht-Text | AA | teilweise | Fokusrahmen und Feldränder ausreichend; Rand der Eingabefelder (`gray-300`) liegt knapp unter 3:1, Änderung mit der Gestaltung abstimmen. |
| 1.4.12 Textabstand | AA | erfüllt | Keine festen Höhen an Textblöcken. |
| 1.4.13 Eingeblendeter Inhalt | AA | erfüllt | Menüs öffnen per Klick, schließen mit Escape oder erneutem Klick; keine Hover-Tooltips mit Inhalt außer `title`. |
| 2.1.1 Tastatur | A | erfüllt | Alle Funktionen per Tastatur; Datumsfeld als Textfeld, Kalender optional; breite Tabellen sind als scrollbarer Bereich fokussierbar. Ausnahme: Kamera-Scan, Ersatz ist die Eingabe der Nummer. |
| 2.1.2 Keine Tastaturfalle | A | erfüllt | |
| 2.1.4 Tastaturkürzel | A | nicht anwendbar | Keine Einzeltasten-Kürzel. |
| 2.2.1 Zeitbegrenzungen anpassbar | A | teilweise | Sitzungen laufen nach Inaktivität ab (Sicherheitsanforderung); vorher eingegebene Formulardaten gehen dann verloren. Warnung vor Ablauf fehlt. |
| 2.2.2 Pausieren, beenden, ausblenden | A | erfüllt | Keine bewegten Inhalte. |
| 2.3.1 Dreimaliges Blitzen | A | erfüllt | |
| 2.4.1 Bereiche überspringen | A | erfüllt | Sprunglink „Zum Inhalt springen“ als erstes Element. |
| 2.4.2 Seite mit Titel | A | erfüllt | Jede Seite hat einen eigenen Titel. |
| 2.4.3 Fokus-Reihenfolge | A | erfüllt | Nach HTMX-Austausch springt der Fokus auf den neuen Inhalt, wenn das fokussierte Element ersetzt wurde. |
| 2.4.4 Linkzweck im Kontext | A | erfüllt | |
| 2.4.5 Mehrere Wege | AA | erfüllt | Hauptmenü, Suche im Seitenkopf, „Was liegt an“ auf der Startseite. |
| 2.4.6 Überschriften und Beschriftungen | AA | erfüllt | |
| 2.4.7 Fokus sichtbar | AA | erfüllt | 3 px Rahmen in Blau, in der roten Kopfzeile weiß, im Dunkelmodus hellblau. |
| 2.5.1 Zeigergesten | A | erfüllt | Keine Wisch- oder Mehrfingergesten. |
| 2.5.2 Zeigerabbruch | A | erfüllt | Standard-Bedienelemente. |
| 2.5.3 Beschriftung im Namen | A | erfüllt | axe-Regel `label-content-name-mismatch` ohne Befund. |
| 2.5.4 Auslösung durch Bewegung | A | nicht anwendbar | |
| 3.1.1 Sprache der Seite | A | erfüllt | `lang="de"`. |
| 3.1.2 Sprache von Teilen | AA | erfüllt | Keine fremdsprachigen Abschnitte. |
| 3.2.1 Bei Fokus | A | erfüllt | |
| 3.2.2 Bei Eingabe | A | teilweise | Einige Filter-Auswahllisten senden beim Ändern sofort ab (z. B. Haushaltsjahr, Bereich). Mit der Tastatur Liste mit Alt + Pfeil nach unten öffnen; besser: Knopf „Anzeigen“ ergänzen. |
| 3.2.3 Konsistente Navigation | AA | erfüllt | Ein Layout für alle Seiten. |
| 3.2.4 Konsistente Erkennung | AA | erfüllt | |
| 3.3.1 Fehlererkennung | A | erfüllt | Fehler als Text am Feld und oben auf der Seite. |
| 3.3.2 Beschriftungen oder Anweisungen | A | erfüllt | Formate wie „TT.MM.JJJJ“ und „HH:MM“ stehen am Feld. |
| 3.3.3 Fehlerempfehlung | AA | erfüllt | Meldungen sagen, was erlaubt ist. |
| 3.3.4 Fehlervermeidung (rechtlich, finanziell, Daten) | AA | erfüllt | Kassenbuchungen, Löschungen und Abschlüsse mit Bestätigung oder Storno statt Löschen. |
| 4.1.1 Syntaxanalyse | A | erfüllt | In WCAG 2.2 entfallen; Razor erzeugt gültiges HTML. |
| 4.1.2 Name, Rolle, Wert | A | erfüllt | axe ohne Befund; Auswahllisten ohne sichtbare Beschriftung haben `aria-label`. |
| 4.1.3 Statusmeldungen | AA | teilweise | Erfolgs- und Fehlermeldungen mit `role="status"`/`role="alert"`; Live-Region für HTMX-Inhalte vorhanden, aber nur dort genutzt, wo der Inhalt `data-ansage` trägt. |

## Kapitel 10: Dokumente

| Punkt | Ergebnis | Anmerkung |
|---|---|---|
| PDF-Ausgaben (Dienstbuch, Quittungen, Bescheinigungen, Berichte, Archivpaket) | teilweise | Alle über `PdfAusgabe.AlsPdf` als getaggtes PDF/UA-1 mit Sprache „de-DE“; das Archivpaket zusätzlich PDF/A-3b. Der Dokumenttitel in den Metadaten ist einheitlich „FEVAD – Freiwillige Feuerwehr Adelberg“ statt des Einzeltitels; Tabellenköpfe und Lesereihenfolge nicht mit PAC geprüft. |
| CSV- und Excel-Export | erfüllt | Reine Daten mit Kopfzeile. |
| Hochgeladene Dateien | nicht erfüllt | Liegen in der Verantwortung der Hochladenden; Hinweis in der Erklärung. |

## Kapitel 12: Dokumentation und Support

| Punkt | Ergebnis | Anmerkung |
|---|---|---|
| 12.1 Barrierefreiheitsmerkmale dokumentiert | erfüllt | Erklärung zur Barrierefreiheit unter `/Barrierefreiheit`, ohne Anmeldung erreichbar, verlinkt im Seitenfuß. |
| 12.2 Support | teilweise | Kontakt über die Einstellung `BARRIEREFREIHEIT_KONTAKT`; Ansprechperson muss die Gemeinde benennen. |

## Offene Punkte vor dem Echtbetrieb

1. Kontakt für Rückmeldungen festlegen (Hauptamt) und in `.env` eintragen; Datum der Prüfung in `BARRIEREFREIHEIT_STAND`.
2. Test mit Screenreader (NVDA unter Windows, VoiceOver am iPhone) für die Kernwege der Einsatzkraft: Anmelden, Rückmeldung, eigene Akte, Mangel melden.
3. Auswahllisten mit sofortigem Absenden auf einen Knopf „Anzeigen“ umstellen (3.2.2).
4. Warnung vor Ablauf der Sitzung (2.2.1).
5. PDF-Titel je Dokument setzen und eine Stichprobe mit PAC 2024 prüfen.
6. `autocomplete` an persönlichen Feldern der Mitgliederakte (1.3.5) und Feldränder kräftiger (1.4.11).
7. Prüfung durch eine Fachstelle (BITV-Test) oder zumindest eine zweite Person mit Prüferfahrung.
