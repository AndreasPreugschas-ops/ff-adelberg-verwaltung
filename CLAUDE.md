# Hinweise für die Arbeit an diesem Repository

- Domänensprache ist Deutsch (Klassen, Eigenschaften, Seiten), Umlaute im Code ausgeschrieben (`ae`, `oe`, `ue`, `ss`). Oberflächentexte mit echten Umlauten.
- Zeiten als `DateTimeOffset` in UTC speichern, in der Oberfläche über `Anzeige` in Europe/Berlin und im 24-Stunden-Format anzeigen. Reine Datumswerte als `DateOnly`.
- Datumsfelder als `<input type="date">` schreiben (der `DatumTagHelper` macht daraus Textfeld „TT.MM.JJJJ“ plus Kalenderknopf, Alpine-Werte sind dann „TT.MM.JJJJ“), Uhrzeiten als Textfeld „HH:MM“ mit `fevad-uhrzeit`. Keine Zeiger-Uhr, kein `datetime-local`.
- Bestehende Einträge bearbeiten im Dialog (Standard): Klick auf Zeile oder Eintrag öffnet ein `<dialog class="dialog">` (Öffner `data-dialog="id"`, „Abbrechen“ mit `data-dialog-schliessen`, Aufbau `dialog-kopf`/`dialog-inhalt`/`dialog-fuss`, siehe `Pages/Laufbahn/_EhrungDialog.cshtml`). Speichern oder Abbrechen, kein Springen zu Ankern und keine Inline-Formulare in Tabellen; nach dem Speichern über das Feld `zurueck` auf die aufrufende Seite zurück.
- Fristen, Altersgrenzen und Sätze nie als Konstante im Code, sondern als `Parameter` oder Katalogwert.
- Neue Rechte nur in `Infrastructure/Identitaet/Berechtigungen.cs` eintragen; Seiten prüfen Policies, nicht Rollen.
- Gesundheitsdaten (Atemschutz) nur Status und Frist, nie Befunde.
- Nach Modelländerungen eine Migration anlegen; die CI prüft mit `has-pending-model-changes`.
- Neue Tailwind-Klassen: mit `tools/tailwindcss` bauen und `wwwroot/css/app.css` mit einchecken.
- Scope: Verwaltung und Dokumentation. Keine Alarmierung, keine Einsatzführung.
