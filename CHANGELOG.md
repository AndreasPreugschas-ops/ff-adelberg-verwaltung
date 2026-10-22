# Änderungen

Alle nennenswerten Änderungen an FEVAD. Format nach [Keep a Changelog](https://keepachangelog.com/de/1.1.0/),
Versionsnummern nach [Semantic Versioning](https://semver.org/lang/de/). Ablauf eines Releases: README,
Abschnitt „Versionen und Releases“.

## [Unreleased]

### Neu

- Dokumentenablage für Satzung, Dienstanweisungen, Protokolle und Formulare: Versionen, Sichtbarkeit
  „alle“ oder „nur Führung“, Lesebestätigung je Version mit Übersicht, wer noch fehlt. Dateien liegen in
  PostgreSQL und sind damit im Backup enthalten.
- Ausschuss und Versammlungen: Sitzungen mit Einladungsfrist, Teilnehmern und Beschlussfähigkeit,
  Niederschrift, Beschlüsse mit Nummer `JJJJ/NN` und Wiedervorlage.
- Wahlen nach FwG BW: absolute Mehrheit für Kommandant und Stellvertreter, Mehrheitswahl mit
  Ersatzmitgliedern für den Ausschuss, Zustimmung des Gemeinderats, Übernahme als Funktion mit Amtszeit;
  Übersicht der Ämter mit Warnung vor Ablauf der Amtszeit.

## [0.1.0] – 2026-10-22

Erste Vorabversion zum Ausprobieren, noch nicht für den Echtbetrieb.

### Neu

- Projektgerüst: Mitglieder, Dienste mit Anwesenheit, Fahrzeuge, Datenmodell mit Audit-Protokoll,
  Container-Image und CI (#1).
- Zugänge: deutsche Kontoseiten, Benutzerverwaltung durch den Administrator, Einrichtungslink per
  E-Mail und Zwei-Faktor-Pflicht (#2), lokal abschaltbar über `Anmeldung:ZweiFaktorPflichtAus` (#4).
- Einsatzbericht erfassen, bearbeiten und freigeben; Entwurf automatisch aus dem alamos-FE2-Alarm (#3).
- Name FEVAD in Oberfläche, E-Mails, Authenticator-Eintrag und Container-Image
  `ghcr.io/andreaspreugschas-ops/fevad`; Versionsanzeige in der Fußzeile; Release-Ablauf mit Git-Tags,
  GitHub-Releases und diesem Änderungsprotokoll.

### Geändert

- Solution, Projekte und Namespaces heißen `Fevad.*` statt `FfAdelberg.*`.
- Datenbank, Datenbankbenutzer und Compose-Projekt heißen `fevad`. Lokal entsteht dadurch ein neues
  Docker-Volume mit frischer Datenbank; die alten Daten liegen weiter im Volume `ff-adelberg_postgres-daten`.
- Anmeldecookie und Data-Protection-Name heißen `fevad`: bestehende Anmeldungen und noch offene
  Einrichtungslinks werden einmalig ungültig.

[Unreleased]: https://github.com/AndreasPreugschas-ops/ff-adelberg-verwaltung/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/AndreasPreugschas-ops/ff-adelberg-verwaltung/releases/tag/v0.1.0
