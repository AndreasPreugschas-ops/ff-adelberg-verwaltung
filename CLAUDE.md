# Hinweise für die Arbeit an diesem Repository

- Domänensprache ist Deutsch (Klassen, Eigenschaften, Seiten), Umlaute im Code ausgeschrieben (`ae`, `oe`, `ue`, `ss`). Oberflächentexte mit echten Umlauten.
- Zeiten als `DateTimeOffset` in UTC speichern, in der Oberfläche über `Anzeige` in Europe/Berlin und im 24-Stunden-Format anzeigen. Reine Datumswerte als `DateOnly`.
- Fristen, Altersgrenzen und Sätze nie als Konstante im Code, sondern als `Parameter` oder Katalogwert.
- Neue Rechte nur in `Infrastructure/Identitaet/Berechtigungen.cs` eintragen; Seiten prüfen Policies, nicht Rollen.
- Gesundheitsdaten (Atemschutz) nur Status und Frist, nie Befunde.
- Nach Modelländerungen eine Migration anlegen; die CI prüft mit `has-pending-model-changes`.
- Neue Tailwind-Klassen: mit `tools/tailwindcss` bauen und `wwwroot/css/app.css` mit einchecken.
- Scope: Verwaltung und Dokumentation. Keine Alarmierung, keine Einsatzführung.
